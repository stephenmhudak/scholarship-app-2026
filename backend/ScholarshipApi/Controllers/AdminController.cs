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
public class AdminController(IApplicationService applicationService, ITwoFactorService twoFactorService, QueryFactory db) : ControllerBase
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
            .OrderBy("u.LastName", "u.FirstName")
            .GetAsync<dynamic>();
        return Ok(users);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var email = InputValidation.Email(request.Email);
        InputValidation.Password(request.Password);
        var firstName = InputValidation.Required(request.FirstName, "First name", 100);
        var lastName = InputValidation.Required(request.LastName, "Last name", 100);
        var role = InputValidation.Role(request.Role);

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
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = firstName,
            LastName = lastName,
            Role = role,
            SchoolId = schoolId,
            EmailVerifiedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        return CreatedAtAction(nameof(ListUsers), new { }, new { id });
    }

    [HttpPost("users/{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(string id, [FromBody] ResetPasswordRequest request)
    {
        InputValidation.Password(request.NewPassword);
        var exists = await db.Query("Users").Where("Id", id).ExistsAsync();
        if (!exists) throw new KeyNotFoundException("User not found.");

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
            throw new ArgumentException("The cycle to copy questions from doesn't exist.");

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

        transaction.Commit();
        return CreatedAtAction(nameof(ListCycles), new { }, new { id });
    }

    [HttpPut("cycles/{id}")]
    public async Task<IActionResult> UpdateCycle(string id, [FromBody] CycleRequest request)
    {
        var name = ValidateCycle(request);
        if (!await db.Query("ScholarshipCycles").Where("Id", id).ExistsAsync()) throw new KeyNotFoundException("Cycle not found.");
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

public class CreateUserRequest
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string Role { get; set; } = null!;
    public string? SchoolId { get; set; }
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
