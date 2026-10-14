using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarshipApi.DTOs.Application;
using ScholarshipApi.DTOs.Reference;
using ScholarshipApi.Services;

namespace ScholarshipApi.Controllers;

[ApiController]
[Route("api/applications")]
[Authorize]
public class ApplicationsController(IApplicationService applicationService, IReferenceService referenceService) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!;
    private string UserRole => User.FindFirstValue(ClaimTypes.Role)!;

    [HttpGet("my")]
    [Authorize(Policy = "Applicant")]
    public async Task<IActionResult> GetMy()
    {
        var apps = await applicationService.ListForApplicantAsync(UserId);
        return Ok(apps);
    }

    [HttpGet("current")]
    [Authorize(Policy = "Applicant")]
    public async Task<IActionResult> GetCurrent()
    {
        var apps = await applicationService.ListForApplicantAsync(UserId);
        var latest = apps.OrderByDescending(a => a.CreatedAt).FirstOrDefault();
        if (latest == null) return NotFound(new { error = "Application not found." });
        var app = await applicationService.GetAsync(latest.Id, UserId, UserRole);
        return Ok(app);
    }

    [HttpGet("open-cycles")]
    [Authorize(Policy = "Applicant")]
    public async Task<IActionResult> GetOpenCycles() => Ok(await applicationService.ListOpenCyclesAsync());

    [HttpPost]
    [Authorize(Policy = "Applicant")]
    public async Task<IActionResult> Create([FromBody] CreateApplicationRequest? request)
    {
        var id = await applicationService.CreateAsync(UserId, request?.CycleId);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var app = await applicationService.GetAsync(id, UserId, UserRole);
        return Ok(app);
    }

    [HttpPut("{id}/draft")]
    [Authorize(Policy = "Applicant")]
    public async Task<IActionResult> SaveDraft(string id, [FromBody] SubmitApplicationRequest request)
    {
        await applicationService.SaveDraftAsync(id, UserId, request);
        return NoContent();
    }

    [HttpPost("{id}/submit")]
    [Authorize(Policy = "Applicant")]
    public async Task<IActionResult> Submit(string id)
    {
        await applicationService.SubmitAsync(id, UserId);
        return NoContent();
    }

    [HttpPost("{id}/copy-answers")]
    [Authorize(Policy = "Applicant")]
    public async Task<IActionResult> CopyAnswers(string id, [FromBody] CopyAnswersRequest request) =>
        Ok(await applicationService.CopyAnswersAsync(id, request.SourceApplicationId, UserId));

    // Which references the application needs and the links made for them. Status only: answers go to scorers.
    [HttpGet("{id}/references")]
    public async Task<IActionResult> GetReferences(string id) =>
        Ok(await referenceService.ListForApplicationAsync(id, UserId, UserRole));

    [HttpPost("{id}/references")]
    [Authorize(Policy = "Applicant")]
    public async Task<IActionResult> CreateReference(string id, [FromBody] CreateReferenceRequestBody request) =>
        Ok(await referenceService.CreateRequestAsync(id, UserId, request));

    [HttpDelete("{id}/references/{requestId}")]
    [Authorize(Policy = "Applicant")]
    public async Task<IActionResult> DeleteReference(string id, string requestId)
    {
        await referenceService.DeleteRequestAsync(id, UserId, requestId);
        return NoContent();
    }
}

public class CreateApplicationRequest
{
    /// <summary>Optional when exactly one cycle is open.</summary>
    public string? CycleId { get; set; }
}

public class CopyAnswersRequest
{
    public string SourceApplicationId { get; set; } = null!;
}
