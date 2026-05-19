using ScholarshipApi.DTOs.Reference;

namespace ScholarshipApi.Services;

public interface IReferenceService
{
    Task<IEnumerable<ReferenceDto>> ListForApplicationAsync(string applicationId, string userId, string role);
    Task<ReferenceWithCodeDto> CreateAsync(string applicationId, string applicantId, string? label);
    Task<ReferencePublicDto> ValidateCodeAsync(string plaintextCode);
    Task<ReferenceUploadResponse> UploadDocumentAsync(string plaintextCode, IFormFile file);
}
