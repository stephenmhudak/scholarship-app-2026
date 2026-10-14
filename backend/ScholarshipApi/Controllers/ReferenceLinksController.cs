using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ScholarshipApi.Authorization;
using ScholarshipApi.DTOs.Reference;
using ScholarshipApi.Services;

namespace ScholarshipApi.Controllers;

/// <summary>Public: a referee opens, fills in and signs a reference through its link (/r/CODE). No account needed.</summary>
[ApiController]
[Route("api/reference-links/{code}")]
[EnableRateLimiting(RateLimitPolicies.Public)]
public class ReferenceLinksController(IReferenceService referenceService) : ControllerBase
{
    // Answers (up to 20k characters each) plus a signature image of up to 1 MB, base64-encoded
    private const long MaxSubmissionBytes = 4 * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> Get(string code) => Ok(await referenceService.GetLinkAsync(code));

    [HttpPost("files")]
    [RequestSizeLimit(FileUploadLimits.MaxRequestSizeBytes)]
    public async Task<IActionResult> Upload(string code, IFormFile file, [FromForm] string? questionId)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });
        if (file.Length > FileUploadLimits.MaxFileSizeBytes)
            return BadRequest(new { error = FileUploadLimits.TooLargeMessage });

        return Ok(new { fileId = await referenceService.UploadFileAsync(code, questionId, file) });
    }

    [HttpPost("submit")]
    [RequestSizeLimit(MaxSubmissionBytes)]
    public async Task<IActionResult> Submit(string code, [FromBody] ReferenceSubmission submission)
    {
        await referenceService.SubmitAsync(code, submission);
        return NoContent();
    }
}
