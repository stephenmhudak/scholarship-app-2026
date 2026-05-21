using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using SqlKata.Execution;
using ScholarshipApi.Models;

namespace ScholarshipApi.Controllers;

[ApiController]
[Route("api/schools")]
[Authorize]
public class SchoolsController(QueryFactory db) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!;

    private string? UserSchoolId => User.FindFirstValue("schoolId");
    private bool IsSchoolAdmin => User.IsInRole("school_admin");

    private IActionResult? EnforceSchoolOwnership(string schoolId)
    {
        if (IsSchoolAdmin && UserSchoolId != schoolId)
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

        await db.Query("Schools").Where("Id", id).UpdateAsync(new
        {
            Name = request.Name,
            AddressLine1 = request.AddressLine1,
            AddressLine2 = request.AddressLine2,
            City = request.City,
            State = request.State,
            Zip = request.Zip
        });
        return NoContent();
    }

    [HttpGet("{id}/applicants")]
    [Authorize(Policy = "Staff")]
    public async Task<IActionResult> GetApplicants(string id, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var guard = EnforceSchoolOwnership(id);
        if (guard != null) return guard;

        var query = db.Query("Users as u")
            .LeftJoin("Applications as a", j => j.On("a.ApplicantId", "u.Id"))
            .Select("u.Id", "u.FirstName", "u.LastName", "u.Email", "a.Status", "a.SubmittedAt")
            .Where("u.SchoolId", id)
            .Where("u.Role", "applicant")
            .When(!string.IsNullOrEmpty(search), q =>
                q.Where(inner => inner
                    .WhereLike("u.FirstName", $"%{search}%")
                    .OrWhereLike("u.LastName", $"%{search}%")
                    .OrWhereLike("u.Email", $"%{search}%")))
            .ForPage(page, pageSize);

        var applicants = await query.GetAsync<dynamic>();
        return Ok(applicants);
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

        var emailTaken = await db.Query("Users").Where("Email", request.Email).ExistsAsync();
        if (emailTaken) throw new ArgumentException("Email is already registered.");

        var counselorId = Guid.NewGuid().ToString();
        await db.Query("Users").InsertAsync(new
        {
            Id = counselorId,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Role = "counselor",
            SchoolId = id,
            CreatedAt = DateTime.UtcNow
        });

        return Ok(new { id = counselorId, firstName = request.FirstName, lastName = request.LastName, email = request.Email });
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

        var exists = await db.Query("Users")
            .Where("Id", counselorId)
            .Where("SchoolId", id)
            .Where("Role", "counselor")
            .ExistsAsync();
        if (!exists) throw new KeyNotFoundException("Counselor not found.");

        await db.Query("Users").Where("Id", counselorId).UpdateAsync(new
        {
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword)
        });
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
    public string Password { get; set; } = null!;
}

public class ResetCounselorPasswordRequest
{
    public string NewPassword { get; set; } = null!;
}
