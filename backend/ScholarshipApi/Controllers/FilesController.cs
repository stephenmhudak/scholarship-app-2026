using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlKata.Execution;
using ScholarshipApi.Authorization;
using ScholarshipApi.Models;
using ScholarshipApi.Services;

namespace ScholarshipApi.Controllers;

// Files are read/written/deleted through IFileStorageService, which is R2StorageService when Storage:Provider = "R2".
[ApiController]
[Route("api/files")]
[Authorize]
public class FilesController(IFileStorageService fileStorage, ApplicationAccess access, QueryFactory db) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!;
    private string UserRole => User.FindFirstValue(ClaimTypes.Role)!;

    [HttpPost("upload")]
    [RequestSizeLimit(FileUploadLimits.MaxRequestSizeBytes)]
    public async Task<IActionResult> Upload(IFormFile file, [FromForm] string applicationId, [FromForm] string? questionId)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });
        if (file.Length > FileUploadLimits.MaxFileSizeBytes)
            return BadRequest(new { error = FileUploadLimits.TooLargeMessage });

        // ApplicationFiles.ApplicationId is a foreign key, so the file must belong to a real application.
        var application = await db.Query("Applications").Where("Id", applicationId).FirstOrDefaultAsync<Application>()
            ?? throw new KeyNotFoundException("Application not found.");
        if (application.ApplicantId != UserId || application.Status != "draft")
            throw new UnauthorizedAccessException("You do not have permission to upload files to this application.");
        var cycle = await db.Query("ScholarshipCycles").Where("Id", application.CycleId).FirstOrDefaultAsync<ScholarshipCycle>();
        if (cycle is null || !CycleWindow.IsOpen(cycle, DateTime.UtcNow))
            throw new ArgumentException("This scholarship cycle has closed.");
        if (questionId is not null && !await db.Query("Questions").Where("Id", questionId).Where("CycleId", application.CycleId).WhereNull("ReferenceFormId").ExistsAsync())
            throw new ArgumentException("Question not found in this application's cycle.");

        var storagePath = await fileStorage.StoreAsync(file, "application-files");

        var id = Guid.NewGuid().ToString();
        try
        {
            await db.Query("ApplicationFiles").InsertAsync(new
            {
                Id = id,
                ApplicationId = applicationId,
                QuestionId = questionId,
                FileName = file.FileName,
                StoragePath = storagePath,
                UploadedAt = DateTime.UtcNow
            });
        }
        catch
        {
            // Don't leave an orphaned object in storage if the record couldn't be saved.
            await fileStorage.DeleteAsync(storagePath);
            throw;
        }

        return Ok(new { fileId = id });
    }

    [HttpGet("{fileId}")]
    public async Task<IActionResult> Download(string fileId)
    {
        var record = await GetRecordAsync(fileId);
        await access.EnsureCanViewFileAsync(record, UserId, UserRole);

        var (stream, contentType, fileName) = await fileStorage.RetrieveAsync(record.StoragePath, record.FileName);
        return File(stream, contentType, fileName);
    }

    // Lets the application form show which file is already attached when an applicant resumes a draft.
    [HttpGet("{fileId}/info")]
    public async Task<IActionResult> Info(string fileId)
    {
        var record = await GetRecordAsync(fileId);
        await access.EnsureCanViewFileAsync(record, UserId, UserRole);

        return Ok(new { id = record.Id, fileName = record.FileName, uploadedAt = record.UploadedAt });
    }

    [HttpDelete("{fileId}")]
    public async Task<IActionResult> Delete(string fileId)
    {
        var record = await GetRecordAsync(fileId);

        if (!AppRoles.IsAdmin(UserRole))
        {
            // Applicants may only delete files attached to their own draft application.
            var application = await db.Query("Applications").Where("Id", record.ApplicationId).FirstOrDefaultAsync<Application>();
            if (application is null || application.ApplicantId != UserId || application.Status != "draft")
                throw new UnauthorizedAccessException("You do not have permission to delete this file.");
        }

        // Remove the object first so a storage failure leaves the record intact and the delete can be retried.
        await fileStorage.DeleteAsync(record.StoragePath);
        await db.Query("ApplicationFiles").Where("Id", fileId).DeleteAsync();

        return NoContent();
    }

    private async Task<ApplicationFile> GetRecordAsync(string fileId) =>
        await db.Query("ApplicationFiles").Where("Id", fileId).FirstOrDefaultAsync<ApplicationFile>()
            ?? throw new KeyNotFoundException("File not found.");
}
