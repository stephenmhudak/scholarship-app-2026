using ScholarshipApi.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using SqlKata.Execution;
using ScholarshipApi.Models;
using ScholarshipApi.Services;

namespace ScholarshipApi.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = "AppAdmin")]
public class AdminController(IApplicationService applicationService, ITwoFactorService twoFactorService, IAuthService authService, QueryFactory db) : ControllerBase
{
    private const int MaxPageSize = 100;

    // ── Applications ──────────────────────────────────────────────────────────

    [HttpGet("applications")]
    public async Task<IActionResult> ListApplications(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!string.IsNullOrEmpty(status)) InputValidation.ApplicationStatus(status);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var items = await applicationService.AdminListAsync(status, search, page, pageSize);
        var total = await applicationService.AdminCountAsync(status, search);
        return Ok(new { data = items, total, page, pageSize });
    }

    [HttpPatch("applications/{id}/status")]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateStatusRequest request)
    {
        await applicationService.UpdateStatusAsync(id, request.Status);
        return NoContent();
    }

    // ── Scoring Overview ──────────────────────────────────────────────────────

    [HttpGet("scoring/overview")]
    public async Task<IActionResult> ScoringOverview()
    {
        var apps = (await db.Query("Applications as a")
            .Join("Users as u", "u.Id", "a.ApplicantId")
            .Select("a.Id as ApplicationId", "a.Status", "u.FirstName", "u.LastName", "a.SubmittedAt")
            .Where(ApplicationAccess.Scoreable(DateTime.UtcNow))
            .OrderByDesc("a.SubmittedAt")
            .GetAsync<AppOverviewRow>()).ToList();

        var scoredCounts = (await db.Query("Scores")
            .SelectRaw("ApplicationId, COUNT(DISTINCT ScoredById) as Total")
            .GroupBy("ApplicationId")
            .GetAsync<CountRow>())
            .ToDictionary(r => r.ApplicationId, r => r.Total);

        // One row per section, overall score, or reference ("Reference: Teacher Reference (Ms. Smith)")
        var sectionAverages = (await db.Query("Scores as s")
            .LeftJoin("Sections as sec", "sec.Id", "s.SectionId")
            .LeftJoin("ReferenceRequests as rr", "rr.Id", "s.ReferenceRequestId")
            .LeftJoin("ReferenceForms as rf", "rf.Id", "rr.FormId")
            .SelectRaw("""
                s.ApplicationId, s.SectionId, s.ReferenceRequestId,
                CASE WHEN rr.Id IS NOT NULL
                     THEN CONCAT('Reference: ', rf.Title, IF(rr.Label IS NULL OR rr.Label = '', '', CONCAT(' (', rr.Label, ')')))
                     ELSE COALESCE(sec.Title, 'Ungrouped') END as SectionTitle,
                AVG(s.Score) as Average, COUNT(DISTINCT s.ScoredById) as ScorerCount
                """)
            .GroupBy("s.ApplicationId", "s.SectionId", "s.ReferenceRequestId", "sec.Title", "rr.Id", "rf.Title", "rr.Label")
            .GetAsync<SectionAvgRow>())
            .GroupBy(r => r.ApplicationId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = apps.Select(app =>
        {
            var sections = sectionAverages.GetValueOrDefault(app.ApplicationId, []);
            var overallAvg = sections.Count > 0
                ? Math.Round(sections.Average(s => s.Average), 2)
                : (decimal?)null;

            return new
            {
                applicationId = app.ApplicationId,
                firstName = app.FirstName,
                lastName = app.LastName,
                status = app.Status,
                submittedAt = app.SubmittedAt,
                scoredCount = scoredCounts.GetValueOrDefault(app.ApplicationId, 0),
                overallAverage = overallAvg,
                sectionAverages = sections.Select(s => new
                {
                    sectionId = s.SectionId,
                    referenceRequestId = s.ReferenceRequestId,
                    sectionTitle = s.SectionTitle,
                    average = Math.Round(s.Average, 2),
                    scorerCount = s.ScorerCount
                }).ToList()
            };
        }).ToList();

        return Ok(result);
    }

    // ── Export ────────────────────────────────────────────────────────────────

    [HttpGet("applications/export")]
    public async Task<IActionResult> Export([FromQuery] string? status)
    {
        if (!string.IsNullOrEmpty(status)) InputValidation.ApplicationStatus(status);
        var query = db.Query("Applications as a")
            .Join("Users as u", "u.Id", "a.ApplicantId")
            .Select("a.Id", "a.Status", "u.FirstName", "u.LastName", "u.Email", "a.SubmittedAt", "a.CreatedAt")
            .When(!string.IsNullOrEmpty(status), q => q.Where("a.Status", status));

        var rows = await query.GetAsync<dynamic>();

        var csv = new StringBuilder();
        csv.AppendLine("Id,Status,FirstName,LastName,Email,SubmittedAt,CreatedAt");
        foreach (var row in rows)
            csv.AppendLine(string.Join(",", new object?[] { row.Id, row.Status, row.FirstName, row.LastName, row.Email, row.SubmittedAt, row.CreatedAt }
                .Select(CsvField)));

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "applications.csv");
    }

    // Quotes every field, and defuses values a spreadsheet would run as a formula (names and emails are applicant-entered)
    private static string CsvField(object? value)
    {
        var text = value switch
        {
            null => "",
            DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss"),
            _ => value.ToString() ?? ""
        };
        if (text.Length > 0 && "=+-@\t\r".Contains(text[0])) text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    // ── Scorers ───────────────────────────────────────────────────────────────

    [HttpGet("scorers")]
    public async Task<IActionResult> ListScorers()
    {
        var scorers = await db.Query("Users")
            .Where("Role", "scorer")
            .Select("Id", "FirstName", "LastName", "Email")
            .OrderBy("LastName")
            .GetAsync<dynamic>();
        return Ok(scorers);
    }

    // ── Users ─────────────────────────────────────────────────────────────────

    [HttpGet("users")]
    public async Task<IActionResult> ListUsers()
    {
        var users = await db.Query("Users as u")
            .LeftJoin("Schools as s", "s.Id", "u.SchoolId")
            .Select("u.Id", "u.Email", "u.FirstName", "u.LastName", "u.Role", "u.SchoolId",
                    "s.Name as SchoolName", "u.CreatedAt")
            .SelectRaw("u.TwoFactorMethod IS NOT NULL AS TwoFactorEnabled")
            .SelectRaw("u.PasswordHash = '' AS InvitePending")
            .Select("u.ScoringScope", "u.DeactivatedAt")
            .OrderBy("u.LastName", "u.FirstName")
            .GetAsync<dynamic>();
        return Ok(users);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var email = InputValidation.Email(request.Email);
        var firstName = InputValidation.Required(request.FirstName, "First name", 100);
        var lastName = InputValidation.Required(request.LastName, "Last name", 100);
        var role = InputValidation.Role(request.Role);
        if (role == AppRoles.SuperAdmin && !IsSuperAdmin)
            throw new UnauthorizedAccessException("Only super admins can create super admins.");
        // Counselors are invited by email to create their own password; everyone else gets one from the admin
        var invite = role == "counselor";
        if (!invite) InputValidation.Password(request.Password);

        // School staff belong to a school; nobody else does
        string? schoolId = null;
        if (role is "school_admin" or "counselor")
        {
            if (string.IsNullOrEmpty(request.SchoolId)) throw new ArgumentException("Choose a school for this user.");
            if (!await db.Query("Schools").Where("Id", request.SchoolId).ExistsAsync())
                throw new ArgumentException("That school doesn't exist.");
            schoolId = request.SchoolId;
        }

        var exists = await db.Query("Users").Where("Email", email).ExistsAsync();
        if (exists) throw new ArgumentException("Email is already registered.");

        var id = Guid.NewGuid().ToString();
        await db.Query("Users").InsertAsync(new
        {
            Id = id,
            Email = email,
            PasswordHash = invite ? "" : BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = firstName,
            LastName = lastName,
            Role = role,
            SchoolId = schoolId,
            // Invited users verify their email by using the setup link
            EmailVerifiedAt = invite ? (DateTime?)null : DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        var emailSent = true;
        if (invite)
        {
            try { await authService.SendPasswordSetupEmailAsync(id); }
            catch (ArgumentException) { emailSent = false; }
        }
        return CreatedAtAction(nameof(ListUsers), new { }, new { id, emailSent });
    }

    // Name, email, role and school. A role change ends the user's sessions so their permissions update.
    [HttpPut("users/{id}")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest request)
    {
        await EnsureCanManageAsync(id);
        var user = await db.Query("Users").Where("Id", id).FirstAsync<User>();
        var email = InputValidation.Email(request.Email);
        var firstName = InputValidation.Required(request.FirstName, "First name", 100);
        var lastName = InputValidation.Required(request.LastName, "Last name", 100);
        var role = InputValidation.Role(request.Role);
        if (role != user.Role)
        {
            if (id == CurrentUserId) throw new ArgumentException("You can't change your own role.");
            if (role == AppRoles.SuperAdmin && !IsSuperAdmin)
                throw new UnauthorizedAccessException("Only super admins can make someone a super admin.");
        }

        string? schoolId = null;
        if (role is "school_admin" or "counselor")
        {
            if (string.IsNullOrEmpty(request.SchoolId)) throw new ArgumentException("Choose a school for this user.");
            if (!await db.Query("Schools").Where("Id", request.SchoolId).ExistsAsync())
                throw new ArgumentException("That school doesn't exist.");
            schoolId = request.SchoolId;
        }

        if (!string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase)
            && await db.Query("Users").Where("Email", email).WhereNot("Id", id).ExistsAsync())
            throw new ArgumentException("Email is already registered.");

        await db.Query("Users").Where("Id", id).UpdateAsync(new
        {
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Role = role,
            SchoolId = schoolId,
        });
        // Role and school are in the session token
        if (role != user.Role || schoolId != user.SchoolId) await SessionVersion.BumpAsync(db, id);
        return NoContent();
    }

    // Deactivated users can't sign in and are signed out now; everything they did stays
    [HttpPost("users/{id}/deactivate")]
    public async Task<IActionResult> DeactivateUser(string id)
    {
        await EnsureCanManageAsync(id);
        if (id == CurrentUserId) throw new ArgumentException("You can't deactivate your own account.");
        await db.Query("Users").Where("Id", id).WhereNull("DeactivatedAt").UpdateAsync(new { DeactivatedAt = DateTime.UtcNow });
        await SessionVersion.BumpAsync(db, id);
        return NoContent();
    }

    [HttpPost("users/{id}/reactivate")]
    public async Task<IActionResult> ReactivateUser(string id)
    {
        await EnsureCanManageAsync(id);
        await db.Query("Users").Where("Id", id).UpdateAsync(new { DeactivatedAt = (DateTime?)null });
        return NoContent();
    }

    // ── Scorer scope: which applications a scorer can see and score ──────────

    [HttpGet("users/{id}/scoring-scope")]
    public async Task<IActionResult> GetScoringScope(string id)
    {
        var scorer = await GetScorerAsync(id);
        var filters = await db.Query("ScorerFilters as f")
            .Join("Questions as q", "q.Id", "f.QuestionId")
            .Join("ScholarshipCycles as c", "c.Id", "q.CycleId")
            .LeftJoin("QuestionOptions as o", "o.Id", "f.OptionId")
            .LeftJoin("Schools as s", "s.Id", "f.SchoolId")
            .Where("f.ScorerId", id)
            .OrderBy("c.Name", "q.Order")
            .Select("f.QuestionId", "f.OptionId", "f.SchoolId", "f.TextContains", "q.CycleId", "q.Text as QuestionText",
                    "c.Name as CycleName", "o.Text as OptionText", "s.Name as SchoolName")
            .GetAsync<dynamic>();
        return Ok(new { scope = scorer.ScoringScope, filters });
    }

    [HttpPut("users/{id}/scoring-scope")]
    public async Task<IActionResult> UpdateScoringScope(string id, [FromBody] ScoringScopeRequest request)
    {
        await GetScorerAsync(id);
        if (request.Scope is not ("all" or "filtered")) throw new ArgumentException("Choose which applications this scorer sees.");
        var filters = request.Scope == "filtered" ? request.Filters ?? [] : [];
        if (request.Scope == "filtered" && filters.Count == 0)
            throw new ArgumentException("Add at least one answer to limit this scorer to, or let them see all applications.");

        foreach (var filter in filters)
        {
            var question = await db.Query("Questions").Where("Id", filter.QuestionId ?? "").WhereNull("ReferenceFormId")
                .FirstOrDefaultAsync<Question>()
                ?? throw new ArgumentException("A filter uses a question that isn't on an application.");
            (filter.OptionId, filter.SchoolId, filter.TextContains) =
                await ConditionTarget.ValidateAsync(db, question, filter.OptionId, filter.SchoolId, filter.TextContains);
        }

        if (db.Connection.State != System.Data.ConnectionState.Open) db.Connection.Open();
        using var transaction = db.Connection.BeginTransaction();
        await db.Query("Users").Where("Id", id).UpdateAsync(new { ScoringScope = request.Scope }, transaction);
        await db.Query("ScorerFilters").Where("ScorerId", id).DeleteAsync(transaction);
        foreach (var filter in filters)
        {
            await db.Query("ScorerFilters").InsertAsync(new
            {
                Id = Guid.NewGuid().ToString(),
                ScorerId = id,
                filter.QuestionId,
                filter.OptionId,
                filter.SchoolId,
                filter.TextContains,
            }, transaction);
        }
        transaction.Commit();
        return NoContent();
    }

    private async Task<User> GetScorerAsync(string id)
    {
        var user = await db.Query("Users").Where("Id", id).FirstOrDefaultAsync<User>()
            ?? throw new KeyNotFoundException("User not found.");
        if (user.Role != "scorer") throw new ArgumentException("Only scorers can be limited to certain applications.");
        return user;
    }

    // ── Impersonation ────────────────────────────────────────────────────────

    // Signs the super admin in as this user (their menus, data and permissions) until they end it
    [HttpPost("users/{id}/impersonate")]
    [Authorize(Policy = "SuperAdmin")]
    public async Task<IActionResult> Impersonate(string id) => Ok(await authService.ImpersonateAsync(CurrentUserId, id));

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!;
    private bool IsSuperAdmin => User.IsInRole(AppRoles.SuperAdmin);

    // App admins can't take over a super admin's account (password, 2FA, invitation)
    private async Task EnsureCanManageAsync(string userId)
    {
        var role = await db.Query("Users").Where("Id", userId).Select("Role").FirstOrDefaultAsync<string>()
            ?? throw new KeyNotFoundException("User not found.");
        if (role == AppRoles.SuperAdmin && !IsSuperAdmin)
            throw new UnauthorizedAccessException("Only super admins can manage super admin accounts.");
    }

    [HttpPost("users/{id}/resend-invite")]
    public async Task<IActionResult> ResendInvite(string id)
    {
        await EnsureCanManageAsync(id);
        await authService.SendPasswordSetupEmailAsync(id);
        return NoContent();
    }

    [HttpPost("users/{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(string id, [FromBody] ResetPasswordRequest request)
    {
        InputValidation.Password(request.NewPassword);
        await EnsureCanManageAsync(id);

        await db.Query("Users").Where("Id", id).UpdateAsync(new
        {
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword)
        });
        await SessionVersion.BumpAsync(db, id);
        return NoContent();
    }

    [HttpPost("users/{id}/reset-2fa")]
    public async Task<IActionResult> ResetTwoFactor(string id)
    {
        await EnsureCanManageAsync(id);
        await twoFactorService.ResetByAdminAsync(id);
        return NoContent();
    }

    // ── Schools ───────────────────────────────────────────────────────────────

    [HttpGet("schools")]
    public async Task<IActionResult> AdminListSchools()
    {
        var schools = await db.Query("Schools").OrderBy("Name").GetAsync<School>();
        return Ok(schools);
    }

    [HttpPut("schools/{id}")]
    public async Task<IActionResult> AdminUpdateSchool(string id, [FromBody] AdminSchoolRequest request)
    {
        if (!await db.Query("Schools").Where("Id", id).ExistsAsync()) throw new KeyNotFoundException("School not found.");
        await db.Query("Schools").Where("Id", id).UpdateAsync(SchoolFields.Validate(request.Name, request.AddressLine1,
            request.AddressLine2, request.City, request.State, request.Zip));
        return NoContent();
    }

    // ── School Admin Invites ──────────────────────────────────────────────────

    [HttpPost("invites")]
    public async Task<IActionResult> GenerateSchoolAdminInvite()
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes)
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

        // Only the hash is stored; the link is shown to the admin once, here
        await db.Query("SchoolAdminInvites").InsertAsync(new
        {
            Id = Guid.NewGuid().ToString(),
            TokenHash = TokenHash.Sha256(token),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });

        return Ok(new { token });
    }

    [HttpGet("invites")]
    public async Task<IActionResult> ListInvites()
    {
        var invites = await db.Query("SchoolAdminInvites as i")
            .LeftJoin("Schools as s", "s.Id", "i.SchoolId")
            .Select("i.Id", "s.Name as SchoolName", "i.CreatedAt", "i.ExpiresAt", "i.UsedAt")
            .OrderByDesc("i.CreatedAt")
            .GetAsync<dynamic>();
        return Ok(invites);
    }

    // ── Cycles ────────────────────────────────────────────────────────────────

    [HttpGet("cycles")]
    public async Task<IActionResult> ListCycles()
    {
        var cycles = await db.Query("ScholarshipCycles").OrderByDesc("OpenDate").GetAsync<ScholarshipCycle>();
        return Ok(cycles);
    }

    [HttpPost("cycles")]
    public async Task<IActionResult> CreateCycle([FromBody] CycleRequest request)
    {
        var name = ValidateCycle(request);
        if (!string.IsNullOrEmpty(request.CopyFromCycleId)
            && !await db.Query("ScholarshipCycles").Where("Id", request.CopyFromCycleId).ExistsAsync())
            throw new ArgumentException("The application period to copy questions from doesn't exist.");

        var id = Guid.NewGuid().ToString();
        if (db.Connection.State != System.Data.ConnectionState.Open) db.Connection.Open();
        using var transaction = db.Connection.BeginTransaction();

        await db.Query("ScholarshipCycles").InsertAsync(new
        {
            Id = id,
            Name = name,
            OpenDate = request.OpenDate,
            CloseDate = request.CloseDate,
            IsActive = request.IsActive
        }, transaction);
        if (string.IsNullOrEmpty(request.CopyFromCycleId))
            await DefaultCycleQuestions.SeedAsync(db, id, transaction);
        else
            await CycleQuestionCopier.CopyAsync(db, request.CopyFromCycleId, id, transaction);
        // Conditions only ever look back up the form, even if the copied cycle had older ones that didn't
        await QuestionOrder.PruneConditionsAsync(db, id, transaction);

        transaction.Commit();
        return CreatedAtAction(nameof(ListCycles), new { }, new { id });
    }

    [HttpPut("cycles/{id}")]
    public async Task<IActionResult> UpdateCycle(string id, [FromBody] CycleRequest request)
    {
        var name = ValidateCycle(request);
        if (!await db.Query("ScholarshipCycles").Where("Id", id).ExistsAsync()) throw new KeyNotFoundException("Application period not found.");
        await db.Query("ScholarshipCycles").Where("Id", id).UpdateAsync(new
        {
            Name = name,
            OpenDate = request.OpenDate,
            CloseDate = request.CloseDate,
            IsActive = request.IsActive
        });
        return NoContent();
    }

    private static string ValidateCycle(CycleRequest request)
    {
        var name = InputValidation.Required(request.Name, "Name", 255);
        if (request.OpenDate == default || request.CloseDate == default) throw new ArgumentException("Open and close dates are required.");
        if (request.CloseDate.Date < request.OpenDate.Date) throw new ArgumentException("The close date must be on or after the open date.");
        return name;
    }
}

// ── Request / Row types ───────────────────────────────────────────────────────

public class UpdateStatusRequest { public string Status { get; set; } = null!; }

public class UpdateUserRequest
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Role { get; set; } = null!;
    public string? SchoolId { get; set; }
}

public class CreateUserRequest
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    /// <summary>Not used for counselors, who are emailed a link to create their own.</summary>
    public string? Password { get; set; }
    public string Role { get; set; } = null!;
    public string? SchoolId { get; set; }
}

public class ScoringScopeRequest
{
    /// <summary><c>all</c>, or <c>filtered</c>: only applications matching any of <see cref="Filters"/>.</summary>
    public string? Scope { get; set; }
    public List<ScorerFilterRequest>? Filters { get; set; }
}

public class ScorerFilterRequest
{
    /// <summary>An application question (any cycle).</summary>
    public string? QuestionId { get; set; }
    /// <summary>For choice questions: applications where this option was chosen.</summary>
    public string? OptionId { get; set; }
    /// <summary>For text questions: applications whose answer contains this text (ignoring case).</summary>
    /// <summary>For School Select questions: the school chosen.</summary>
    public string? SchoolId { get; set; }
    public string? TextContains { get; set; }
}

public class ResetPasswordRequest { public string NewPassword { get; set; } = null!; }

public class AdminSchoolRequest
{
    public string Name { get; set; } = null!;
    public string AddressLine1 { get; set; } = null!;
    public string AddressLine2 { get; set; } = null!;
    public string City { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Zip { get; set; } = null!;
}

public class CycleRequest
{
    public string Name { get; set; } = null!;
    public DateTime OpenDate { get; set; }
    public DateTime CloseDate { get; set; }
    public bool IsActive { get; set; }
    /// <summary>Create only: copy questions from this cycle instead of seeding the defaults.</summary>
    public string? CopyFromCycleId { get; set; }
}

internal class AppOverviewRow
{
    public string ApplicationId { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public DateTime? SubmittedAt { get; set; }
}

internal class CountRow
{
    public string ApplicationId { get; set; } = null!;
    public int Total { get; set; }
}

internal class SectionAvgRow
{
    public string ApplicationId { get; set; } = null!;
    public string? SectionId { get; set; }
    public string? ReferenceRequestId { get; set; }
    public string SectionTitle { get; set; } = null!;
    public decimal Average { get; set; }
    public int ScorerCount { get; set; }
}
