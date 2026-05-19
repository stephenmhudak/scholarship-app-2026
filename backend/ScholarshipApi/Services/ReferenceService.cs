using System.Security.Cryptography;
using SqlKata.Execution;
using ScholarshipApi.DTOs.Reference;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

public class ReferenceService(QueryFactory db, IFileStorageService fileStorage) : IReferenceService
{
    private static readonly HashSet<string> AllowedExtensions = [".doc", ".docx", ".pdf"];
    private static readonly HashSet<string> AllowedMimeTypes = [
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/pdf"
    ];

    public async Task<IEnumerable<ReferenceDto>> ListForApplicationAsync(string applicationId, string userId, string role)
    {
        var app = await db.Query("Applications").Where("Id", applicationId).FirstOrDefaultAsync<Application>()
            ?? throw new KeyNotFoundException("Application not found.");

        if (role == "applicant" && app.ApplicantId != userId)
            throw new UnauthorizedAccessException("Access denied.");

        return await db.Query("References").Where("ApplicationId", applicationId).GetAsync<ReferenceDto>();
    }

    public async Task<ReferenceWithCodeDto> CreateAsync(string applicationId, string applicantId, string? label)
    {
        var app = await db.Query("Applications").Where("Id", applicationId).FirstOrDefaultAsync<Application>()
            ?? throw new KeyNotFoundException("Application not found.");

        if (app.ApplicantId != applicantId) throw new UnauthorizedAccessException("Access denied.");

        var cycle = await db.Query("ScholarshipCycles").Where("Id", app.CycleId).FirstAsync<ScholarshipCycle>();

        var plaintext = GenerateCode();
        var hashed = HashCode(plaintext);
        var id = Guid.NewGuid().ToString();

        await db.Query("References").InsertAsync(new
        {
            Id = id,
            ApplicationId = applicationId,
            Code = hashed,
            Label = label,
            Status = "pending",
            ExpiresAt = cycle.CloseDate,
            CreatedAt = DateTime.UtcNow
        });

        var dto = await db.Query("References").Where("Id", id).FirstAsync<ReferenceDto>();
        return new ReferenceWithCodeDto
        {
            Id = dto.Id,
            ApplicationId = dto.ApplicationId,
            Label = dto.Label,
            Status = dto.Status,
            ExpiresAt = dto.ExpiresAt,
            CreatedAt = dto.CreatedAt,
            PlaintextCode = plaintext
        };
    }

    public async Task<ReferencePublicDto> ValidateCodeAsync(string plaintextCode)
    {
        var hashed = HashCode(plaintextCode);
        var reference = await db.Query("References").Where("Code", hashed).FirstOrDefaultAsync<Reference>()
            ?? throw new KeyNotFoundException("Reference code not found.");

        if (reference.ExpiresAt < DateTime.UtcNow)
            throw new ArgumentException("Reference code has expired.");

        var app = await db.Query("Applications as a")
            .Join("Users as u", "u.Id", "a.ApplicantId")
            .Select("u.FirstName", "u.LastName")
            .Where("a.Id", reference.ApplicationId)
            .FirstAsync<dynamic>();

        return new ReferencePublicDto
        {
            ApplicantName = $"{app.FirstName} {app.LastName}",
            Label = reference.Label
        };
    }

    public async Task<ReferenceUploadResponse> UploadDocumentAsync(string plaintextCode, IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new ArgumentException("Only .doc, .docx, and .pdf files are allowed.");
        if (!AllowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
            throw new ArgumentException("Invalid file type.");

        var hashed = HashCode(plaintextCode);
        var reference = await db.Query("References").Where("Code", hashed).FirstOrDefaultAsync<Reference>()
            ?? throw new KeyNotFoundException("Reference code not found.");

        if (reference.ExpiresAt < DateTime.UtcNow)
            throw new ArgumentException("Reference code has expired.");
        if (reference.Status != "pending")
            throw new ArgumentException("Reference has already been submitted.");

        var storagePath = await fileStorage.StoreAsync(file, "references");

        await db.Query("ReferenceDocuments").InsertAsync(new
        {
            Id = Guid.NewGuid().ToString(),
            ReferenceId = reference.Id,
            FileName = file.FileName,
            StoragePath = storagePath,
            UploadedAt = DateTime.UtcNow
        });

        await db.Query("References").Where("Id", reference.Id).UpdateAsync(new { Status = "received" });

        return new ReferenceUploadResponse
        {
            ReferenceId = reference.Id,
            FileName = file.FileName,
            Message = "Reference document received successfully."
        };
    }

    private static string GenerateCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToHexString(bytes).ToLower();
    }

    private static string HashCode(string plaintext)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToHexString(bytes).ToLower();
    }
}
