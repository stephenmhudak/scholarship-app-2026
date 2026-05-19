using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlKata.Execution;
using ScholarshipApi.Models;
using ScholarshipApi.Services;

namespace ScholarshipApi.Controllers;

[ApiController]
[Route("api/files")]
[Authorize]
public class FilesController(IFileStorageService fileStorage, QueryFactory db) : ControllerBase
{
    [HttpPost("upload")]
    [RequestSizeLimit(10_485_760)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        var storagePath = await fileStorage.StoreAsync(file, "application-files");

        var id = Guid.NewGuid().ToString();
        await db.Query("ApplicationFiles").InsertAsync(new
        {
            Id = id,
            ApplicationId = "pending",
            FileName = file.FileName,
            StoragePath = storagePath,
            UploadedAt = DateTime.UtcNow
        });

        return Ok(new { fileId = id });
    }

    [HttpGet("{fileId}")]
    public async Task<IActionResult> Download(string fileId)
    {
        var record = await db.Query("ApplicationFiles").Where("Id", fileId).FirstOrDefaultAsync<ApplicationFile>()
            ?? throw new KeyNotFoundException("File not found.");

        var (stream, contentType, fileName) = await fileStorage.RetrieveAsync(record.StoragePath, record.FileName);
        return File(stream, contentType, fileName);
    }
}
