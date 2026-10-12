using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ScholarshipApi.Authorization;
using ScholarshipApi.Services;

namespace ScholarshipApi.Controllers;

[ApiController]
[Route("api/references")]
[EnableRateLimiting(RateLimitPolicies.Public)]
public class ReferencesController(IReferenceService referenceService) : ControllerBase
{
    [HttpGet("{code}")]
    public async Task<IActionResult> ValidateCode(string code)
    {
        var result = await referenceService.ValidateCodeAsync(code);
        return Ok(result);
    }

    [HttpPost("{code}/upload")]
    [RequestSizeLimit(FileUploadLimits.MaxRequestSizeBytes)]
    public async Task<IActionResult> Upload(string code, IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });
        if (file.Length > FileUploadLimits.MaxFileSizeBytes)
            return BadRequest(new { error = FileUploadLimits.TooLargeMessage });

        var result = await referenceService.UploadDocumentAsync(code, file);
        return Ok(result);
    }
}
