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
public class AdminController(IApplicationService applicationService, QueryFactory db) : ControllerBase
{
    // ── Applications ──────────────────────────────────────────────────────────

    [HttpGet("applications")]
    public async Task<IActionResult> ListApplications(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
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

    [HttpPost("applications/{id}/assign")]
    public async Task<IActionResult> AssignScorer(string id, [FromBody] AssignScorerRequest request)
    {
        await applicationService.AssignScorerAsync(id, request.ScoredById);
        return NoContent();
    }

    [HttpGet("applications/{id}/scorers")]
    public async Task<IActionResult> GetAssignedScorers(string id)
    {
        var scorers = await db.Query("ApplicationScorers as aps")
            .Join("Users as u", "u.Id", "aps.ScoredById")
            .Select("u.Id", "u.FirstName", "u.LastName", "u.Email", "aps.AssignedAt")
            .Where("aps.ApplicationId", id)
            .OrderBy("u.LastName")
            .GetAsync<dynamic>();
        return Ok(scorers);
    }

    // ── Scoring Overview ──────────────────────────────────────────────────────

    [HttpGet("scoring/overview")]
    public async Task<IActionResult> ScoringOverview()
    {
        var apps = (await db.Query("Applications as a")
            .Join("Users as u", "u.Id", "a.ApplicantId")
            .Select("a.Id as ApplicationId", "a.Status", "u.FirstName", "u.LastName", "a.SubmittedAt")
            .WhereNot("a.Status", "draft")
            .OrderByDesc("a.SubmittedAt")
            .GetAsync<AppOverviewRow>()).ToList();

        var assignedCounts = (await db.Query("ApplicationScorers")
            .SelectRaw("ApplicationId, COUNT(*) as Total")
            .GroupBy("ApplicationId")
            .GetAsync<CountRow>())
            .ToDictionary(r => r.ApplicationId, r => r.Total);

        var scoredCounts = (await db.Query("Scores")
            .SelectRaw("ApplicationId, COUNT(DISTINCT ScoredById) as Total")
            .GroupBy("ApplicationId")
            .GetAsync<CountRow>())
            .ToDictionary(r => r.ApplicationId, r => r.Total);

        var sectionAverages = (await db.Query("Scores as s")
            .LeftJoin("Sections as sec", "sec.Id", "s.SectionId")
            .SelectRaw("s.ApplicationId, s.SectionId, COALESCE(sec.Title, 'Ungrouped') as SectionTitle, AVG(s.Score) as Average, COUNT(DISTINCT s.ScoredById) as ScorerCount")
            .GroupBy("s.ApplicationId", "s.SectionId", "sec.Title")
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
                assignedCount = assignedCounts.GetValueOrDefault(app.ApplicationId, 0),
                scoredCount = scoredCounts.GetValueOrDefault(app.ApplicationId, 0),
                overallAverage = overallAvg,
                sectionAverages = sections.Select(s => new
                {
                    sectionId = s.SectionId,
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
        var query = db.Query("Applications as a")
            .Join("Users as u", "u.Id", "a.ApplicantId")
            .Select("a.Id", "a.Status", "u.FirstName", "u.LastName", "u.Email", "a.SubmittedAt", "a.CreatedAt")
            .When(!string.IsNullOrEmpty(status), q => q.Where("a.Status", status));

        var rows = await query.GetAsync<dynamic>();

        var csv = new StringBuilder();
        csv.AppendLine("Id,Status,FirstName,LastName,Email,SubmittedAt,CreatedAt");
        foreach (var row in rows)
            csv.AppendLine($"{row.Id},{row.Status},{row.FirstName},{row.LastName},{row.Email},{row.SubmittedAt},{row.CreatedAt}");

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "applications.csv");
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
            .OrderBy("u.LastName", "u.FirstName")
            .GetAsync<dynamic>();
        return Ok(users);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var exists = await db.Query("Users").Where("Email", request.Email).ExistsAsync();
        if (exists) throw new ArgumentException("Email is already registered.");

        var id = Guid.NewGuid().ToString();
        await db.Query("Users").InsertAsync(new
        {
            Id = id,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Role = request.Role,
            SchoolId = string.IsNullOrEmpty(request.SchoolId) ? null : request.SchoolId,
            CreatedAt = DateTime.UtcNow
        });
        return CreatedAtAction(nameof(ListUsers), new { }, new { id });
    }

    [HttpPost("users/{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(string id, [FromBody] ResetPasswordRequest request)
    {
        var exists = await db.Query("Users").Where("Id", id).ExistsAsync();
        if (!exists) throw new KeyNotFoundException("User not found.");

        await db.Query("Users").Where("Id", id).UpdateAsync(new
        {
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword)
        });
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

    // ── School Admin Invites ──────────────────────────────────────────────────

    [HttpPost("invites")]
    public async Task<IActionResult> GenerateSchoolAdminInvite()
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes)
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

        await db.Query("SchoolAdminInvites").InsertAsync(new
        {
            Id = Guid.NewGuid().ToString(),
            Token = token,
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
            .Select("i.Id", "i.Token", "s.Name as SchoolName", "i.CreatedAt", "i.ExpiresAt", "i.UsedAt")
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
        var id = Guid.NewGuid().ToString();
        await db.Query("ScholarshipCycles").InsertAsync(new
        {
            Id = id,
            Name = request.Name,
            OpenDate = request.OpenDate,
            CloseDate = request.CloseDate,
            IsActive = request.IsActive
        });
        return CreatedAtAction(nameof(ListCycles), new { }, new { id });
    }

    [HttpPut("cycles/{id}")]
    public async Task<IActionResult> UpdateCycle(string id, [FromBody] CycleRequest request)
    {
        await db.Query("ScholarshipCycles").Where("Id", id).UpdateAsync(new
        {
            Name = request.Name,
            OpenDate = request.OpenDate,
            CloseDate = request.CloseDate,
            IsActive = request.IsActive
        });
        return NoContent();
    }
}

// ── Request / Row types ───────────────────────────────────────────────────────

public class UpdateStatusRequest { public string Status { get; set; } = null!; }
public class AssignScorerRequest { public string ScoredById { get; set; } = null!; }

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
    public string SectionTitle { get; set; } = null!;
    public decimal Average { get; set; }
    public int ScorerCount { get; set; }
}
