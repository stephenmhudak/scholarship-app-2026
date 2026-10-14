using ScholarshipApi.DTOs.Reference;

namespace ScholarshipApi.Services;

public interface IReferenceService
{
    // Applicant: which references the application needs and the links made for them (status only)
    Task<ApplicationReferencesDto> ListForApplicationAsync(string applicationId, string userId, string role);
    Task<ReferenceRequestDto> CreateRequestAsync(string applicationId, string applicantId, CreateReferenceRequestBody body);
    Task DeleteRequestAsync(string applicationId, string applicantId, string requestId);

    // Referee, through the public link
    Task<ReferenceLinkDto> GetLinkAsync(string code);
    Task<string> UploadFileAsync(string code, string? questionId, IFormFile file);
    Task SubmitAsync(string code, ReferenceSubmission submission);

    // Scorer
    Task<List<SubmittedReferenceDto>> ListSubmittedAsync(string applicationId, string userId, string role);
    Task<(Stream Stream, string ContentType, string FileName)> GetSignatureAsync(string requestId, string userId, string role);
    Task<(Stream Stream, string ContentType, string FileName)> GetFileAsync(string fileId, string userId, string role);
}
