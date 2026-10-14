using SqlKata;
using SqlKata.Execution;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

/// <summary>
/// Decides who may read an application and what hangs off it (answers, references, files), and which
/// applications can be scored. Anything not listed here is denied, so a new role sees nothing until it's added.
/// </summary>
public class ApplicationAccess(QueryFactory db)
{
    private static readonly string[] SubmittedStatuses = ["submitted", "under_review", "awarded", "rejected"];
    private static readonly string[] UnsubmittedStatuses = ["draft", "archived"];

    public async Task<bool> CanViewAsync(string applicationId, string applicantId, string status, string cycleId, string userId, string role) => role switch
    {
        "app_admin" => true,
        "scorer" => await IsScoreableAsync(status, cycleId) && await ScorerMaySeeAsync(userId, applicationId),
        "applicant" => applicantId == userId,
        _ => false,
    };

    /// <summary>Submitted applications can be scored any time; unsubmitted ones once their cycle has ended.</summary>
    public async Task<bool> IsScoreableAsync(string status, string cycleId)
    {
        if (SubmittedStatuses.Contains(status)) return true;
        if (!UnsubmittedStatuses.Contains(status)) return false;
        var cycle = await db.Query("ScholarshipCycles").Where("Id", cycleId).FirstOrDefaultAsync<ScholarshipCycle>();
        return cycle is not null && CycleWindow.IsClosed(cycle, DateTime.UtcNow);
    }

    /// <summary>
    /// Of these applications, the ones a scorer limited to certain answers may see, or null when they aren't limited.
    /// A limited scorer sees an application when its answers match any one of their filters.
    /// </summary>
    public async Task<HashSet<string>?> ScorerAllowedAsync(string scorerId, IReadOnlyCollection<string> applicationIds)
    {
        var scope = await db.Query("Users").Where("Id", scorerId).Select("ScoringScope").FirstOrDefaultAsync<string>();
        if (scope != "filtered") return null;

        var filters = (await db.Query("ScorerFilters").Where("ScorerId", scorerId).GetAsync<ScorerFilter>()).ToList();
        if (filters.Count == 0 || applicationIds.Count == 0) return [];

        var answers = (await db.Query("ApplicationAnswers")
                .WhereIn("ApplicationId", applicationIds)
                .WhereIn("QuestionId", filters.Select(f => f.QuestionId).Distinct())
                .GetAsync<ApplicationAnswer>())
            .ToLookup(a => a.ApplicationId);
        return applicationIds.Where(id =>
        {
            var byQuestion = answers[id].GroupBy(a => a.QuestionId).ToDictionary(g => g.Key, g => AnswerValue.From(g.First()));
            return filters.Any(f => AnswerConditions.Matches(f.OptionId, f.TextContains, byQuestion.GetValueOrDefault(f.QuestionId)));
        }).ToHashSet();
    }

    public async Task<bool> ScorerMaySeeAsync(string scorerId, string applicationId) =>
        await ScorerAllowedAsync(scorerId, [applicationId]) is not { } allowed || allowed.Contains(applicationId);

    /// <summary>The <see cref="IsScoreableAsync"/> rule as a filter on a query over "Applications as a".</summary>
    public static Func<Query, Query> Scoreable(DateTime nowUtc) => q => q
        .WhereIn("a.Status", SubmittedStatuses)
        .OrWhere(unsubmitted => unsubmitted
            .WhereIn("a.Status", UnsubmittedStatuses)
            .WhereIn("a.CycleId", CycleWindow.ClosedCycleIds(nowUtc)));

    public async Task EnsureCanViewFileAsync(ApplicationFile file, string userId, string role)
    {
        // Copying answers between cycles carries the file id into the newer application,
        // so a file is also visible through any application that uses it as an answer.
        var apps = await db.Query("Applications")
            .Select("Id", "ApplicantId", "Status", "CycleId")
            .Where("Id", file.ApplicationId)
            .OrWhereIn("Id", db.Query("ApplicationAnswers").Where("TextValue", file.Id).Select("ApplicationId"))
            .GetAsync<Application>();

        foreach (var app in apps)
            if (await CanViewAsync(app.Id, app.ApplicantId, app.Status, app.CycleId, userId, role)) return;
        throw new UnauthorizedAccessException("You do not have permission to view this file.");
    }
}
