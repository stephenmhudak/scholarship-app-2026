using ScholarshipApi.DTOs.Application;

namespace ScholarshipApi.Services;

public interface IApplicationService
{
    Task<IEnumerable<ApplicationDto>> ListForApplicantAsync(string applicantId);
    Task<ApplicationDto> GetAsync(string id, string requestingUserId, string requestingRole);
    Task<string> CreateAsync(string applicantId);
    Task SaveDraftAsync(string id, string applicantId, SubmitApplicationRequest request);
    Task SubmitAsync(string id, string applicantId);
    Task<IEnumerable<ApplicationListDto>> AdminListAsync(string? status, string? search, int page, int pageSize);
    Task<int> AdminCountAsync(string? status, string? search);
    Task UpdateStatusAsync(string id, string status);
    Task AssignScorerAsync(string id, string scorerId);
}
