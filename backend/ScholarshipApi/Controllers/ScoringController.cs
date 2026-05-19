using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarshipApi.DTOs.Scoring;
using ScholarshipApi.Services;

namespace ScholarshipApi.Controllers;

[ApiController]
[Route("api/scoring")]
[Authorize(Policy = "Scorer")]
public class ScoringController(IScoringService scoringService) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!;

    [HttpGet("queue")]
    public async Task<IActionResult> GetQueue()
    {
        var queue = await scoringService.GetQueueAsync(UserId);
        return Ok(queue);
    }

    [HttpGet("{applicationId}")]
    public async Task<IActionResult> GetApplication(string applicationId)
    {
        var app = await scoringService.GetForScoringAsync(applicationId, UserId);
        return Ok(app);
    }

    [HttpPost("{applicationId}/score")]
    public async Task<IActionResult> SubmitScore(string applicationId, [FromBody] SubmitScoreRequest request)
    {
        await scoringService.SubmitScoreAsync(applicationId, UserId, request);
        return NoContent();
    }
}
