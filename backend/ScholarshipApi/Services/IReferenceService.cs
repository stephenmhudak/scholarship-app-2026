using ScholarshipApi.DTOs.Reference;

namespace ScholarshipApi.Services;

public interface IReferenceService
{
    // Applicant: which references the application needs and the links made for them (status only)
    Task<ApplicationReferencesDto> ListForApplicationAsync(string applicationId, string userId, string role);
    Task<ReferenceRequestDto> CreateRequestAsync(string applicationId, string applicantId, CreateReferenceRequestBody body);
    Task DeleteRequestAsync(string applicationId, string applicantId, string requestId);

    // Referee (signed in), through the code
    Task<ReferenceLinkDto> GetLinkAsync(string code, string userId);
    Task<string> UploadFileAsync(string code, string userId, string? questionId, IFormFile file);
    Task SubmitAsync(string code, string userId, ReferenceSubmission submission);
    Task<List<SubmittedReferenceDto>> ListMineAsync(string userId);
    Task<SubmittedReferenceDto> GetMineAsync(string requestId, string userId);

    // Scorer; signature and files also for the referee who submitted them
    Task<List<SubmittedReferenceDto>> ListSubmittedAsync(string applicationId, string userId, string role);
    Task<(Stream Stream, string ContentType, string FileName)> GetSignatureAsync(string requestId, string userId, string role);
    Task<(Stream Stream, string ContentType, string FileName)> GetFileAsync(string fileId, string userId, string role);
}
