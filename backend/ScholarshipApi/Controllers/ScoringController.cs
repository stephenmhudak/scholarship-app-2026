using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarshipApi.Authorization;
using ScholarshipApi.DTOs.Scoring;
using ScholarshipApi.Services;

namespace ScholarshipApi.Controllers;

[ApiController]
[Route("api/scoring")]
[Authorize(Policy = "ScorerOrAdmin")]
public class ScoringController(IScoringService scoringService, IReferenceService referenceService) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!;

    private bool IsAdmin => AppRoles.IsAdmin(User.FindFirstValue(ClaimTypes.Role));
    private string UserRole => User.FindFirstValue(ClaimTypes.Role)!;

    [HttpGet("queue")]
    [Authorize(Policy = "Scorer")]
    public async Task<IActionResult> GetQueue()
    {
        var queue = await scoringService.GetQueueAsync(UserId);
        return Ok(queue);
    }

    [HttpGet("{applicationId}")]
    public async Task<IActionResult> GetApplication(string applicationId)
    {
        var app = await scoringService.GetForScoringAsync(applicationId, UserId, IsAdmin);
        return Ok(app);
    }

    [HttpGet("{applicationId}/my-scores")]
    public async Task<IActionResult> GetMyScores(string applicationId)
    {
        var scores = await scoringService.GetMyScoresAsync(applicationId, UserId, IsAdmin);
        return Ok(scores);
    }

    [HttpPost("{applicationId}/score")]
    public async Task<IActionResult> SubmitScore(string applicationId, [FromBody] SubmitScoreRequest request)
    {
        await scoringService.SubmitScoreAsync(applicationId, UserId, request, IsAdmin);
        return NoContent();
    }

    // ── References: submitted answers, signature and files, for scoring only ──

    [HttpGet("{applicationId}/references")]
    public async Task<IActionResult> GetReferences(string applicationId) =>
        Ok(await referenceService.ListSubmittedAsync(applicationId, UserId, UserRole));

    [HttpGet("references/{requestId}/signature")]
    public async Task<IActionResult> GetReferenceSignature(string requestId)
    {
        var (stream, contentType, _) = await referenceService.GetSignatureAsync(requestId, UserId, UserRole);
        return File(stream, contentType);
    }

    [HttpGet("references/files/{fileId}")]
    public async Task<IActionResult> GetReferenceFile(string fileId)
    {
        var (stream, contentType, fileName) = await referenceService.GetFileAsync(fileId, UserId, UserRole);
        return File(stream, contentType, fileName);
    }
}
