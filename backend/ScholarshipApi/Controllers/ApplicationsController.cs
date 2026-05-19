using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarshipApi.DTOs.Application;
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

    [HttpPost]
    [Authorize(Policy = "Applicant")]
    public async Task<IActionResult> Create()
    {
        var id = await applicationService.CreateAsync(UserId);
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

    [HttpGet("{id}/references")]
    public async Task<IActionResult> GetReferences(string id)
    {
        var refs = await referenceService.ListForApplicationAsync(id, UserId, UserRole);
        return Ok(refs);
    }

    [HttpPost("{id}/references")]
    [Authorize(Policy = "Applicant")]
    public async Task<IActionResult> CreateReference(string id, [FromBody] CreateReferenceRequest request)
    {
        var result = await referenceService.CreateAsync(id, UserId, request.Label);
        return Ok(result);
    }
}

public class CreateReferenceRequest
{
    public string? Label { get; set; }
}
