using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using SqlKata.Execution;
using ScholarshipApi.Authorization;
using ScholarshipApi.Models;
using ScholarshipApi.Services;

namespace ScholarshipApi.Controllers;

[ApiController]
[Route("api/schools")]
[Authorize]
public class SchoolsController(QueryFactory db, ITwoFactorService twoFactorService, IAuthService authService) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!;

    private string? UserSchoolId => User.FindFirstValue("schoolId");
    private bool IsSchoolAdmin => User.IsInRole("school_admin");

    // App admins see every school; everyone else (school admins, counselors, scorers) only their own.
    private IActionResult? EnforceSchoolOwnership(string schoolId)
    {
        if (!AppRoles.IsAdmin(User.FindFirstValue(ClaimTypes.Role)) && UserSchoolId != schoolId)
            return Forbid();
        return null;
    }

    // ── Schools ───────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var query = db.Query("Schools").OrderBy("Name");
        if (IsSchoolAdmin && UserSchoolId != null)
            query = query.Where("Id", UserSchoolId);

        var schools = await query.GetAsync<School>();
        return Ok(schools);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var guard = EnforceSchoolOwnership(id);
        if (guard != null) return guard;

        var school = await db.Query("Schools").Where("Id", id).FirstOrDefaultAsync<School>()
            ?? throw new KeyNotFoundException("School not found.");
        return Ok(school);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "SchoolAdmin")]
    public async Task<IActionResult> Update(string id, [FromBody] SchoolRequest request)
    {
        var guard = EnforceSchoolOwnership(id);
        if (guard != null) return guard;

        var updated = await db.Query("Schools").Where("Id", id).UpdateAsync(SchoolFields.Validate(request.Name, request.AddressLine1,
            request.AddressLine2, request.City, request.State, request.Zip));
        if (updated == 0 && !await db.Query("Schools").Where("Id", id).ExistsAsync()) throw new KeyNotFoundException("School not found.");
        return NoContent();
    }

    // ── Counselors ────────────────────────────────────────────────────────────

    [HttpGet("{id}/counselors")]
    [Authorize(Policy = "Staff")]
    public async Task<IActionResult> GetCounselors(string id)
    {
        var guard = EnforceSchoolOwnership(id);
        if (guard != null) return guard;

        var counselors = await db.Query("Users")
            .Where("Role", "counselor")
            .Where("SchoolId", id)
            .Select("Id", "FirstName", "LastName", "Email", "CreatedAt")
            .SelectRaw("TwoFactorMethod IS NOT NULL AS TwoFactorEnabled")
            // Invited but hasn't created a password from the emailed link yet
            .SelectRaw("PasswordHash = '' AS InvitePending")
            .OrderBy("LastName")
            .GetAsync<dynamic>();
        return Ok(counselors);
    }

    [HttpPost("{id}/counselors")]
    [Authorize(Policy = "SchoolAdmin")]
    public async Task<IActionResult> AddCounselor(string id, [FromBody] AddCounselorRequest request)
    {
        var guard = EnforceSchoolOwnership(id);
        if (guard != null) return guard;

        var schoolExists = await db.Query("Schools").Where("Id", id).ExistsAsync();
        if (!schoolExists) throw new KeyNotFoundException("School not found.");

        var email = InputValidation.Email(request.Email);
        var firstName = InputValidation.Required(request.FirstName, "First name", 100);
        var lastName = InputValidation.Required(request.LastName, "Last name", 100);

        var emailTaken = await db.Query("Users").Where("Email", email).ExistsAsync();
        if (emailTaken) throw new ArgumentException("Email is already registered.");

        var counselorId = Guid.NewGuid().ToString();
        await db.Query("Users").InsertAsync(new
        {
            Id = counselorId,
            Email = email,
            // No password: they create one from the emailed link, and can't sign in until they do.
            // Using the link also verifies their email.
            PasswordHash = "",
            FirstName = firstName,
            LastName = lastName,
            Role = "counselor",
            SchoolId = id,
            CreatedAt = DateTime.UtcNow
        });

        // The counselor is added either way; if the email fails, the school admin can resend it
        var emailSent = true;
        try { await authService.SendPasswordSetupEmailAsync(counselorId); }
        catch (ArgumentException) { emailSent = false; }

        return Ok(new { id = counselorId, firstName, lastName, email, emailSent });
    }

    [HttpPost("{id}/counselors/{counselorId}/resend-invite")]
    [Authorize(Policy = "SchoolAdmin")]
    public async Task<IActionResult> ResendCounselorInvite(string id, string counselorId)
    {
        var guard = EnforceSchoolOwnership(id);
        if (guard != null) return guard;

        var exists = await db.Query("Users")
            .Where("Id", counselorId)
            .Where("SchoolId", id)
            .Where("Role", "counselor")
            .ExistsAsync();
        if (!exists) throw new KeyNotFoundException("Counselor not found.");

        await authService.SendPasswordSetupEmailAsync(counselorId);
        return NoContent();
    }

    [HttpDelete("{id}/counselors/{counselorId}")]
    [Authorize(Policy = "SchoolAdmin")]
    public async Task<IActionResult> RemoveCounselor(string id, string counselorId)
    {
        var guard = EnforceSchoolOwnership(id);
        if (guard != null) return guard;

        var counselor = await db.Query("Users")
            .Where("Id", counselorId)
            .Where("SchoolId", id)
            .Where("Role", "counselor")
            .FirstOrDefaultAsync<dynamic>()
            ?? throw new KeyNotFoundException("Counselor not found.");

        await db.Query("Users").Where("Id", counselorId).DeleteAsync();
        return NoContent();
    }

    [HttpPost("{id}/counselors/{counselorId}/reset-password")]
    [Authorize(Policy = "SchoolAdmin")]
    public async Task<IActionResult> ResetCounselorPassword(string id, string counselorId, [FromBody] ResetCounselorPasswordRequest request)
    {
        var guard = EnforceSchoolOwnership(id);
        if (guard != null) return guard;
        InputValidation.Password(request.NewPassword);

        var counselor = await db.Query("Users")
            .Where("Id", counselorId)
            .Where("SchoolId", id)
            .Where("Role", "counselor")
            .FirstOrDefaultAsync<User>()
            ?? throw new KeyNotFoundException("Counselor not found.");
        // Counselors choose their own first password
        if (!AuthService.HasPassword(counselor))
            throw new ArgumentException("This counselor hasn't created their password yet. Resend their invitation instead.");

        await db.Query("Users").Where("Id", counselorId).UpdateAsync(new
        {
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword)
        });
        await SessionVersion.BumpAsync(db, counselorId);
        return NoContent();
    }

    [HttpPost("{id}/counselors/{counselorId}/reset-2fa")]
    [Authorize(Policy = "SchoolAdmin")]
    public async Task<IActionResult> ResetCounselorTwoFactor(string id, string counselorId)
    {
        var guard = EnforceSchoolOwnership(id);
        if (guard != null) return guard;

        var exists = await db.Query("Users")
            .Where("Id", counselorId)
            .Where("SchoolId", id)
            .Where("Role", "counselor")
            .ExistsAsync();
        if (!exists) throw new KeyNotFoundException("Counselor not found.");

        await twoFactorService.ResetByAdminAsync(counselorId);
        return NoContent();
    }
}

public class SchoolRequest
{
    public string Name { get; set; } = null!;
    public string AddressLine1 { get; set; } = null!;
    public string AddressLine2 { get; set; } = null!;
    public string City { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Zip { get; set; } = null!;
}

public class AddCounselorRequest
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
}

public class ResetCounselorPasswordRequest
{
    public string NewPassword { get; set; } = null!;
}
