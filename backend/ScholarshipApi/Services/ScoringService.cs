using SqlKata.Execution;
using ScholarshipApi.DTOs.Application;
using ScholarshipApi.DTOs.Scoring;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

public class ScoringService(QueryFactory db, IApplicationService applicationService) : IScoringService
{
    public async Task<IEnumerable<ScoringQueueItemDto>> GetQueueAsync(string scorerId)
    {
        var apps = (await db.Query("Applications as a")
            .Join("Users as u", "u.Id", "a.ApplicantId")
            .Select("a.Id", "a.CycleId", "a.Status", "u.FirstName", "u.LastName", "a.SubmittedAt")
            .WhereNot("a.Status", "draft")
            .OrderByDesc("a.SubmittedAt")
            .GetAsync<ScoringQueueItemDto>()).ToList();

        if (!apps.Any()) return apps;

        var cycleIds = apps.Select(a => a.CycleId).Distinct().ToList();
        var sectionsPerCycle = (await db.Query("Sections")
            .SelectRaw("CycleId, COUNT(*) as Total")
            .WhereIn("CycleId", cycleIds)
            .GroupBy("CycleId")
            .GetAsync<CycleSectionCountRow>())
            .ToDictionary(r => r.CycleId, r => r.Total);

        var appIds = apps.Select(a => a.Id).ToList();
        var scoredByApp = (await db.Query("Scores")
            .Select("ApplicationId", "SectionId")
            .Where("ScoredById", scorerId)
            .WhereIn("ApplicationId", appIds)
            .GetAsync<ScoreRowCompact>())
            .GroupBy(r => r.ApplicationId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => r.SectionId).ToHashSet()
            );

        var result = new List<ScoringQueueItemDto>();
        foreach (var app in apps)
        {
            var totalSections = sectionsPerCycle.GetValueOrDefault(app.CycleId, 0);
            var scored = scoredByApp.GetValueOrDefault(app.Id, []);

            bool fullyScored = totalSections == 0
                ? scored.Contains(null)
                : scored.Count(id => id != null) >= totalSections;

            if (!fullyScored)
            {
                app.HasScored = scored.Any();
                result.Add(app);
            }
        }

        return result;
    }

    public async Task<ApplicationDto> GetForScoringAsync(string applicationId, string scorerId, bool isAdmin = false)
    {
        return await applicationService.GetAsync(applicationId, scorerId, isAdmin ? "app_admin" : "scorer");
    }

    public async Task SubmitScoreAsync(string applicationId, string scorerId, SubmitScoreRequest request, bool isAdmin = false)
    {
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
        return await db.Query("Scores as s")
            .LeftJoin("Sections as sec", "sec.Id", "s.SectionId")
            .Select("s.Id", "s.ApplicationId", "s.ScoredById", "s.SectionId",
                    "s.Score as ScoreValue", "s.Comments", "s.ScoredAt", "sec.Title as SectionTitle")
            .Where("s.ApplicationId", applicationId)
            .Where("s.ScoredById", scorerId)
            .GetAsync<ScoreDto>();
    }

    private class CycleSectionCountRow
    {
        public string CycleId { get; set; } = null!;
        public int Total { get; set; }
    }

    private class ScoreRowCompact
    {
        public string ApplicationId { get; set; } = null!;
        public string? SectionId { get; set; }
    }
}
