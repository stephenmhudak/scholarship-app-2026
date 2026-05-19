using SqlKata.Execution;
using ScholarshipApi.DTOs.Application;
using ScholarshipApi.DTOs.Scoring;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

public class ScoringService(QueryFactory db, IApplicationService applicationService) : IScoringService
{
    public async Task<IEnumerable<ApplicationListDto>> GetQueueAsync(string scorerId)
    {
        return await db.Query("Applications as a")
            .Join("Users as u", "u.Id", "a.ApplicantId")
            .Join("Scores as s", "s.ApplicationId", "a.Id")
            .Select("a.Id", "a.Status", "u.FirstName", "u.LastName", "a.SubmittedAt", "a.CreatedAt")
            .Where("s.ScoredById", scorerId)
            .GetAsync<ApplicationListDto>();
    }

    public async Task<ApplicationDto> GetForScoringAsync(string applicationId, string scorerId)
    {
        var isAssigned = await db.Query("Scores")
            .Where("ApplicationId", applicationId)
            .Where("ScoredById", scorerId)
            .ExistsAsync();

        if (!isAssigned) throw new UnauthorizedAccessException("You are not assigned to score this application.");

        return await applicationService.GetAsync(applicationId, scorerId, "scorer");
    }

    public async Task SubmitScoreAsync(string applicationId, string scorerId, SubmitScoreRequest request)
    {
        var scoreRow = await db.Query("Scores")
            .Where("ApplicationId", applicationId)
            .Where("ScoredById", scorerId)
            .FirstOrDefaultAsync<Score>()
            ?? throw new UnauthorizedAccessException("You are not assigned to score this application.");

        await db.Query("Scores").Where("Id", scoreRow.Id).UpdateAsync(new
        {
            Score = request.Score,
            Comments = request.Comments,
            ScoredAt = DateTime.UtcNow
        });
    }
}
