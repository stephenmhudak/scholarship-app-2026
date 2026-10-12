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

    public async Task<bool> CanViewAsync(string applicantId, string status, string cycleId, string userId, string role) => role switch
    {
        "app_admin" => true,
        "scorer" => await IsScoreableAsync(status, cycleId),
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
            .Select("ApplicantId", "Status", "CycleId")
            .Where("Id", file.ApplicationId)
            .OrWhereIn("Id", db.Query("ApplicationAnswers").Where("TextValue", file.Id).Select("ApplicationId"))
            .GetAsync<Application>();

        foreach (var app in apps)
            if (await CanViewAsync(app.ApplicantId, app.Status, app.CycleId, userId, role)) return;
        throw new UnauthorizedAccessException("You do not have permission to view this file.");
    }
}
