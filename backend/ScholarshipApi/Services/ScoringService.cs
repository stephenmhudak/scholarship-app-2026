using SqlKata.Execution;
using ScholarshipApi.DTOs.Application;
using ScholarshipApi.DTOs.Scoring;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

public class ScoringService(QueryFactory db, IApplicationService applicationService, ApplicationAccess access) : IScoringService
{
    // Mirrors the limits on frontend/src/components/scoring/ScoringForm.vue
    private const decimal MinScore = 1;
    private const decimal MaxScore = 100;
    private const int MaxCommentLength = 5000;

    public async Task<IEnumerable<ScoringQueueItemDto>> GetQueueAsync(string scorerId)
    {
        var apps = (await db.Query("Applications as a")
            .Join("Users as u", "u.Id", "a.ApplicantId")
            .Select("a.Id", "a.CycleId", "a.Status", "u.FirstName", "u.LastName", "a.SubmittedAt")
            .Where(ApplicationAccess.Scoreable(DateTime.UtcNow))
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
        var app = await db.Query("Applications").Where("Id", applicationId).FirstOrDefaultAsync<Application>()
            ?? throw new KeyNotFoundException("Application not found.");
        if (!await access.IsScoreableAsync(app.Status, app.CycleId))
            throw new ArgumentException("This application can't be scored until it's submitted or its cycle has ended.");

        var sectionScores = request.SectionScores ?? [];
        if (sectionScores.Count == 0) throw new ArgumentException("Enter at least one score.");
        if (sectionScores.GroupBy(s => s.SectionId).Any(g => g.Count() > 1))
            throw new ArgumentException("Each section can only be scored once.");

        // Cycles with sections are scored per section; cycles without get one overall score (no section)
        var sectionIds = (await db.Query("Sections").Where("CycleId", app.CycleId).Select("Id").GetAsync<string>()).ToHashSet();
        foreach (var s in sectionScores)
        {
            var validSection = sectionIds.Count == 0 ? s.SectionId is null : s.SectionId is not null && sectionIds.Contains(s.SectionId);
            if (!validSection) throw new ArgumentException("A score is for a section that isn't part of this application.");
            if (s.Score < MinScore || s.Score > MaxScore)
                throw new ArgumentException($"Scores must be between {MinScore} and {MaxScore}.");
            if (s.Comments?.Length > MaxCommentLength)
                throw new ArgumentException($"Comments must be at most {MaxCommentLength} characters.");
        }

        await db.Query("Scores")
            .Where("ApplicationId", applicationId)
            .Where("ScoredById", scorerId)
            .DeleteAsync();

        var now = DateTime.UtcNow;
        foreach (var s in sectionScores)
        {
            await db.Query("Scores").InsertAsync(new
            {
                Id = Guid.NewGuid().ToString(),
                ApplicationId = applicationId,
                ScoredById = scorerId,
                SectionId = s.SectionId,
                Score = Math.Round(s.Score, 2),
                Comments = string.IsNullOrWhiteSpace(s.Comments) ? null : s.Comments.Trim(),
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
