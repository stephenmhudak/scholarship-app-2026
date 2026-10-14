using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ScholarshipApi.Authorization;
using ScholarshipApi.DTOs.Reference;
using ScholarshipApi.Services;

namespace ScholarshipApi.Controllers;

/// <summary>
/// A referee opens, fills in and signs a reference by its 10-character code (from the /r/CODE link, or typed in).
/// Needs a referee, counselor or school admin account; the submission is tied to that account.
/// </summary>
[ApiController]
[Route("api/reference-links/{code}")]
[Authorize(Policy = "Referee")]
[EnableRateLimiting(RateLimitPolicies.Public)]
public class ReferenceLinksController(IReferenceService referenceService) : ControllerBase
{
    // Answers (up to 20k characters each) plus a signature image of up to 1 MB, base64-encoded
    private const long MaxSubmissionBytes = 4 * 1024 * 1024;

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!;

    [HttpGet]
    public async Task<IActionResult> Get(string code) => Ok(await referenceService.GetLinkAsync(code, UserId));

    [HttpPost("files")]
    [RequestSizeLimit(FileUploadLimits.MaxRequestSizeBytes)]
    public async Task<IActionResult> Upload(string code, IFormFile file, [FromForm] string? questionId)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });
        if (file.Length > FileUploadLimits.MaxFileSizeBytes)
            return BadRequest(new { error = FileUploadLimits.TooLargeMessage });

        return Ok(new { fileId = await referenceService.UploadFileAsync(code, UserId, questionId, file) });
    }

    [HttpPost("submit")]
    [RequestSizeLimit(MaxSubmissionBytes)]
    public async Task<IActionResult> Submit(string code, [FromBody] ReferenceSubmission submission)
    {
        await referenceService.SubmitAsync(code, UserId, submission);
        return NoContent();
    }
}
