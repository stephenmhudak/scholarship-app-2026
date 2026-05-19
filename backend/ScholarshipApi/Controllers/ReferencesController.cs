using Microsoft.AspNetCore.Mvc;
using ScholarshipApi.Services;

namespace ScholarshipApi.Controllers;

[ApiController]
[Route("api/references")]
public class ReferencesController(IReferenceService referenceService) : ControllerBase
{
    [HttpGet("{code}")]
    public async Task<IActionResult> ValidateCode(string code)
    {
        var result = await referenceService.ValidateCodeAsync(code);
        return Ok(result);
    }

    [HttpPost("{code}/upload")]
    [RequestSizeLimit(10_485_760)]
    public async Task<IActionResult> Upload(string code, IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        var result = await referenceService.UploadDocumentAsync(code, file);
        return Ok(result);
    }
}
