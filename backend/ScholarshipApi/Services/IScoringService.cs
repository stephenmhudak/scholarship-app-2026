using ScholarshipApi.DTOs.Application;
using ScholarshipApi.DTOs.Scoring;

namespace ScholarshipApi.Services;

public interface IScoringService
{
    Task<IEnumerable<ScoringQueueItemDto>> GetQueueAsync(string scorerId);
    Task<ApplicationDto> GetForScoringAsync(string applicationId, string scorerId, bool isAdmin = false);
    Task SubmitScoreAsync(string applicationId, string scorerId, SubmitScoreRequest request, bool isAdmin = false);
    Task<IEnumerable<ScoreDto>> GetMyScoresAsync(string applicationId, string scorerId, bool isAdmin = false);
}
