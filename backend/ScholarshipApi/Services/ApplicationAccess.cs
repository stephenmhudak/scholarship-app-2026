using SqlKata.Execution;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

/// <summary>
/// Decides who may read an application and what hangs off it (answers, references, files).
/// Anything not listed here is denied, so a new role sees nothing until it's added.
/// </summary>
public class ApplicationAccess(QueryFactory db)
{
    // Scorers only see applications once they've been submitted, never drafts or archived drafts.
    private static readonly HashSet<string> ScorableStatuses = ["submitted", "under_review", "awarded", "rejected"];

    public static bool CanView(string applicantId, string status, string userId, string role) => role switch
    {
        "app_admin" => true,
        "scorer" => ScorableStatuses.Contains(status),
        "applicant" => applicantId == userId,
        _ => false,
    };

    public async Task EnsureCanViewFileAsync(ApplicationFile file, string userId, string role)
    {
        // Copying answers between cycles carries the file id into the newer application,
        // so a file is also visible through any application that uses it as an answer.
        var apps = await db.Query("Applications")
            .Select("ApplicantId", "Status")
            .Where("Id", file.ApplicationId)
            .OrWhereIn("Id", db.Query("ApplicationAnswers").Where("TextValue", file.Id).Select("ApplicationId"))
            .GetAsync<Application>();

        if (!apps.Any(a => CanView(a.ApplicantId, a.Status, userId, role)))
            throw new UnauthorizedAccessException("You do not have permission to view this file.");
    }
}
