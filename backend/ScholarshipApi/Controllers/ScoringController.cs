using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarshipApi.DTOs.Scoring;
using ScholarshipApi.Services;

namespace ScholarshipApi.Controllers;

[ApiController]
[Route("api/scoring")]
[Authorize(Policy = "ScorerOrAdmin")]
public class ScoringController(IScoringService scoringService) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!;

    private bool IsAdmin => User.IsInRole("app_admin");

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
}
