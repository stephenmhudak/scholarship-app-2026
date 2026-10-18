using ScholarshipApi.DTOs.Application;

namespace ScholarshipApi.Services;

public interface IApplicationService
{
    Task<IEnumerable<ApplicationDto>> ListForApplicantAsync(string applicantId);
    Task<ApplicationDto> GetAsync(string id, string requestingUserId, string requestingRole);
    Task<IEnumerable<OpenCycleDto>> ListOpenCyclesAsync();
    Task<string> CreateAsync(string applicantId, string? cycleId);
    Task<CopyAnswersResult> CopyAnswersAsync(string targetId, string sourceId, string applicantId);
    Task SaveDraftAsync(string id, string applicantId, SubmitApplicationRequest request);
    Task SubmitAsync(string id, string applicantId);
    Task<IEnumerable<ApplicationListDto>> AdminListAsync(AdminApplicationFilter filter, int page, int pageSize);
    Task<int> AdminCountAsync(AdminApplicationFilter filter);
    Task UpdateStatusAsync(string id, string status);
}
