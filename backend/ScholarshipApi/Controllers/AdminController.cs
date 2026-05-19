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
