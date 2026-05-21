using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        {
            csv.AppendLine($"{row.Id},{row.Status},{row.FirstName},{row.LastName},{row.Email},{row.SubmittedAt},{row.CreatedAt}");
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "applications.csv");
    }

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

public class UpdateStatusRequest { public string Status { get; set; } = null!; }
public class AssignScorerRequest { public string ScoredById { get; set; } = null!; }
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
