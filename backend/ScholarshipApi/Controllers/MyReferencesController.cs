using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarshipApi.Services;

namespace ScholarshipApi.Controllers;

/// <summary>The references the signed-in referee submitted: who they were for and what was written. Never scores.</summary>
[ApiController]
[Route("api/my-references")]
[Authorize(Policy = "Referee")]
public class MyReferencesController(IReferenceService referenceService) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!;
    private string UserRole => User.FindFirstValue(ClaimTypes.Role)!;

    [HttpGet]
    public async Task<IActionResult> List() => Ok(await referenceService.ListMineAsync(UserId));

    [HttpGet("{requestId}")]
    public async Task<IActionResult> Get(string requestId) => Ok(await referenceService.GetMineAsync(requestId, UserId));

    [HttpGet("{requestId}/signature")]
    public async Task<IActionResult> GetSignature(string requestId)
    {
        var (stream, contentType, _) = await referenceService.GetSignatureAsync(requestId, UserId, UserRole);
        return File(stream, contentType);
    }

    [HttpGet("files/{fileId}")]
    public async Task<IActionResult> GetFile(string fileId)
    {
        var (stream, contentType, fileName) = await referenceService.GetFileAsync(fileId, UserId, UserRole);
        return File(stream, contentType, fileName);
    }
}
