using SqlKata.Execution;
using ScholarshipApi.DTOs.Application;
using ScholarshipApi.DTOs.Scoring;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

public class ScoringService(QueryFactory db, IApplicationService applicationService) : IScoringService
{
    public async Task<IEnumerable<ScoringQueueItemDto>> GetQueueAsync(string scorerId)
    {
        var assignments = (await db.Query("Applications as a")
            .Join("Users as u", "u.Id", "a.ApplicantId")
            .Join("ApplicationScorers as aps", "aps.ApplicationId", "a.Id")
            .Select("a.Id", "a.Status", "u.FirstName", "u.LastName", "a.SubmittedAt")
            .Where("aps.ScoredById", scorerId)
            .GetAsync<ScoringQueueItemDto>()).ToList();

        var scoredIds = (await db.Query("Scores")
            .Select("ApplicationId")
            .Where("ScoredById", scorerId)
            .GetAsync<dynamic>())
            .Select(r => (string)r.ApplicationId)
            .ToHashSet();

        foreach (var item in assignments)
            item.HasScored = scoredIds.Contains(item.Id);

        return assignments;
    }

    public async Task<ApplicationDto> GetForScoringAsync(string applicationId, string scorerId, bool isAdmin = false)
    {
        if (!isAdmin)
        {
            var isAssigned = await db.Query("ApplicationScorers")
                .Where("ApplicationId", applicationId)
                .Where("ScoredById", scorerId)
                .ExistsAsync();

            if (!isAssigned) throw new UnauthorizedAccessException("You are not assigned to score this application.");
        }

        return await applicationService.GetAsync(applicationId, scorerId, isAdmin ? "app_admin" : "scorer");
    }

    public async Task SubmitScoreAsync(string applicationId, string scorerId, SubmitScoreRequest request, bool isAdmin = false)
    {
        if (!isAdmin)
        {
            var isAssigned = await db.Query("ApplicationScorers")
                .Where("ApplicationId", applicationId)
                .Where("ScoredById", scorerId)
                .ExistsAsync();

            if (!isAssigned) throw new UnauthorizedAccessException("You are not assigned to score this application.");
        }
        else
        {
            var alreadyTracked = await db.Query("ApplicationScorers")
                .Where("ApplicationId", applicationId)
                .Where("ScoredById", scorerId)
                .ExistsAsync();

            if (!alreadyTracked)
                await db.Query("ApplicationScorers").InsertAsync(new
                {
                    Id = Guid.NewGuid().ToString(),
                    ApplicationId = applicationId,
                    ScoredById = scorerId,
                    AssignedAt = DateTime.UtcNow
                });
        }

        await db.Query("Scores")
            .Where("ApplicationId", applicationId)
            .Where("ScoredById", scorerId)
            .DeleteAsync();

        var now = DateTime.UtcNow;
        foreach (var s in request.SectionScores)
        {
            await db.Query("Scores").InsertAsync(new
            {
                Id = Guid.NewGuid().ToString(),
                ApplicationId = applicationId,
                ScoredById = scorerId,
                SectionId = s.SectionId,
                Score = s.Score,
                Comments = s.Comments,
                ScoredAt = now
            });
        }
    }

    public async Task<IEnumerable<ScoreDto>> GetMyScoresAsync(string applicationId, string scorerId, bool isAdmin = false)
    {
        if (!isAdmin)
        {
            var isAssigned = await db.Query("ApplicationScorers")
                .Where("ApplicationId", applicationId)
                .Where("ScoredById", scorerId)
                .ExistsAsync();

            if (!isAssigned) throw new UnauthorizedAccessException("You are not assigned to score this application.");
        }

        return await db.Query("Scores as s")
            .LeftJoin("Sections as sec", "sec.Id", "s.SectionId")
            .Select("s.Id", "s.ApplicationId", "s.ScoredById", "s.SectionId",
                    "s.Score as ScoreValue", "s.Comments", "s.ScoredAt", "sec.Title as SectionTitle")
            .Where("s.ApplicationId", applicationId)
            .Where("s.ScoredById", scorerId)
            .GetAsync<ScoreDto>();
    }
}
