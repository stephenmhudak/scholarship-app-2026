using ScholarshipApi.DTOs.Application;
using ScholarshipApi.DTOs.Scoring;

namespace ScholarshipApi.Services;

public interface IScoringService
{
    Task<IEnumerable<ApplicationListDto>> GetQueueAsync(string scorerId);
    Task<ApplicationDto> GetForScoringAsync(string applicationId, string scorerId);
    Task SubmitScoreAsync(string applicationId, string scorerId, SubmitScoreRequest request);
}
