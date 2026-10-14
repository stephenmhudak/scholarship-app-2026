using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlKata.Execution;
using ScholarshipApi.Authorization;
using ScholarshipApi.Models;
using ScholarshipApi.Services;

namespace ScholarshipApi.Controllers;

/// <summary>
/// Awarding scholarships. Anyone assigned to a scholarship (ScholarshipAwarders) can award it to eligible
/// applications; super admins can award any. Checked on every request, so changes to assignments apply at once.
/// </summary>
[ApiController]
[Route("api/awards")]
[Authorize]
public class AwardsController(QueryFactory db) : ControllerBase
{
    // Applications that can be awarded: submitted ones, including ones already under review or awarded
    private static readonly string[] AwardableStatuses = ["submitted", "under_review", "awarded"];
    private const decimal MaxAmount = 1_000_000_000m;

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!;
    private bool IsSuperAdmin => User.IsInRole(AppRoles.SuperAdmin);

    // The scholarships the signed-in user can award
    [HttpGet("scholarships")]
    public async Task<IActionResult> Scholarships()
    {
        var scholarships = await db.Query("Scholarships as s")
            .Join("ScholarshipCycles as c", "c.Id", "s.CycleId")
            .When(!IsSuperAdmin, q => q.WhereIn("s.Id", db.Query("ScholarshipAwarders").Where("UserId", UserId).Select("ScholarshipId")))
            .OrderByDesc("c.OpenDate").OrderBy("s.Order", "s.CreatedAt")
            .Select("s.Id", "s.Name", "s.Description", "s.Eligibility", "s.CycleId", "c.Name as CycleName")
            .SelectRaw("(SELECT COUNT(*) FROM ScholarshipAwards w WHERE w.ScholarshipId = s.Id) AS AwardCount")
            .GetAsync<dynamic>();
        return Ok(scholarships);
    }

    // Applications eligible for the scholarship, with their average score and any award they already have
    [HttpGet("scholarships/{scholarshipId}/candidates")]
    public async Task<IActionResult> Candidates(string scholarshipId)
    {
        var scholarship = await GetAwardableAsync(scholarshipId);
        var apps = (await db.Query("Applications as a")
            .Join("Users as u", "u.Id", "a.ApplicantId")
            .Where("a.CycleId", scholarship.CycleId)
            .WhereIn("a.Status", AwardableStatuses)
            .Select("a.Id", "a.Status", "a.SubmittedAt", "a.ApplicantId", "u.FirstName", "u.LastName", "u.Email", "u.SchoolId")
            .GetAsync<CandidateRow>()).ToList();
        if (apps.Count == 0) return Ok(Array.Empty<object>());

        var appIds = apps.Select(a => a.Id).ToList();
        var eligible = await EligibleIdsAsync(scholarship, appIds);
        apps = apps.Where(a => eligible.Contains(a.Id)).ToList();
        if (apps.Count == 0) return Ok(Array.Empty<object>());
        appIds = apps.Select(a => a.Id).ToList();

        var averages = (await db.Query("Scores").WhereIn("ApplicationId", appIds).GroupBy("ApplicationId")
                .SelectRaw("ApplicationId, AVG(Score) AS Average").GetAsync<ScoreAverage>())
            .ToDictionary(r => r.ApplicationId, r => Math.Round(r.Average, 2));
        var awards = (await db.Query("ScholarshipAwards as w").LeftJoin("Users as u", "u.Id", "w.AwardedById")
                .Where("w.ScholarshipId", scholarshipId).WhereIn("w.ApplicationId", appIds)
                .Select("w.ApplicationId", "w.Amount", "w.Note", "w.AwardedAt", "u.FirstName", "u.LastName")
                .GetAsync<AwardRow>())
            .ToDictionary(w => w.ApplicationId);
        var schools = await SchoolNamesAsync(apps, scholarship.CycleId);

        return Ok(apps
            .OrderByDescending(a => averages.GetValueOrDefault(a.Id, -1))
            .ThenBy(a => a.LastName).ThenBy(a => a.FirstName)
            .Select(a =>
            {
                awards.TryGetValue(a.Id, out var award);
                return new
                {
                    ApplicationId = a.Id,
                    ApplicantName = $"{a.FirstName} {a.LastName}".Trim(),
                    a.Email,
                    SchoolName = schools.GetValueOrDefault(a.Id),
                    a.Status,
                    a.SubmittedAt,
                    AverageScore = averages.TryGetValue(a.Id, out var avg) ? avg : (decimal?)null,
                    Award = award is null ? null : new
                    {
                        award.Amount,
                        award.Note,
                        award.AwardedAt,
                        AwardedBy = $"{award.FirstName} {award.LastName}".Trim(),
                    },
                };
            }));
    }

    // Awards the scholarship to an eligible application (or updates the amount/note if it already has it)
    [HttpPost("scholarships/{scholarshipId}/awards")]
    public async Task<IActionResult> Award(string scholarshipId, [FromBody] AwardRequest request)
    {
        var scholarship = await GetAwardableAsync(scholarshipId);
        var app = await db.Query("Applications").Where("Id", request.ApplicationId ?? "").Where("CycleId", scholarship.CycleId)
            .FirstOrDefaultAsync<Application>() ?? throw new KeyNotFoundException("Application not found.");
        if (!AwardableStatuses.Contains(app.Status)) throw new ArgumentException("Only submitted applications can be awarded scholarships.");
        if (!(await EligibleIdsAsync(scholarship, [app.Id])).Contains(app.Id))
            throw new ArgumentException("This applicant isn't eligible for this scholarship.");
        if (request.Amount is < 0 or > MaxAmount) throw new ArgumentException("Enter a valid amount.");
        var note = InputValidation.Optional(request.Note, "Note", 2000);
        var amount = request.Amount is { } a ? Math.Round(a, 2) : (decimal?)null;

        var updated = await db.Query("ScholarshipAwards").Where("ScholarshipId", scholarshipId).Where("ApplicationId", app.Id)
            .UpdateAsync(new { Amount = amount, Note = note });
        if (updated == 0)
        {
            await db.Query("ScholarshipAwards").InsertAsync(new
            {
                Id = Guid.NewGuid().ToString(),
                ScholarshipId = scholarshipId,
                ApplicationId = app.Id,
                Amount = amount,
                Note = note,
                AwardedById = UserId,
                AwardedAt = DateTime.UtcNow,
            });
        }
        // An application with at least one scholarship is Awarded
        await db.Query("Applications").Where("Id", app.Id).UpdateAsync(new { Status = "awarded" });
        return NoContent();
    }

    [HttpDelete("scholarships/{scholarshipId}/awards/{applicationId}")]
    public async Task<IActionResult> RemoveAward(string scholarshipId, string applicationId)
    {
        await GetAwardableAsync(scholarshipId);
        var removed = await db.Query("ScholarshipAwards").Where("ScholarshipId", scholarshipId).Where("ApplicationId", applicationId).DeleteAsync();
        if (removed == 0) throw new KeyNotFoundException("That application doesn't have this scholarship.");

        // No scholarships left: back to under review
        if (!await db.Query("ScholarshipAwards").Where("ApplicationId", applicationId).ExistsAsync())
            await db.Query("Applications").Where("Id", applicationId).Where("Status", "awarded").UpdateAsync(new { Status = "under_review" });
        return NoContent();
    }

    private async Task<Scholarship> GetAwardableAsync(string scholarshipId)
    {
        var scholarship = await db.Query("Scholarships").Where("Id", scholarshipId).FirstOrDefaultAsync<Scholarship>()
            ?? throw new KeyNotFoundException("Scholarship not found.");
        if (!IsSuperAdmin && !await db.Query("ScholarshipAwarders").Where("ScholarshipId", scholarshipId).Where("UserId", UserId).ExistsAsync())
            throw new UnauthorizedAccessException("You aren't assigned to award this scholarship.");
        return scholarship;
    }

    // Open scholarships: everyone. Conditional: any/all of its conditions match the saved answers (answers to hidden
    // questions are dropped when saved, so they never count)
    private async Task<HashSet<string>> EligibleIdsAsync(Scholarship scholarship, List<string> applicationIds)
    {
        if (scholarship.Eligibility != "conditional") return applicationIds.ToHashSet();
        var conditions = (await db.Query("ScholarshipConditions").Where("ScholarshipId", scholarship.Id)
            .GetAsync<ScholarshipCondition>()).ToList();
        if (conditions.Count == 0 || applicationIds.Count == 0) return [];

        var answers = (await db.Query("ApplicationAnswers").WhereIn("ApplicationId", applicationIds)
                .WhereIn("QuestionId", conditions.Select(c => c.QuestionId).Distinct())
                .GetAsync<ApplicationAnswer>())
            .ToLookup(a => a.ApplicationId);
        return applicationIds.Where(id =>
        {
            var byQuestion = answers[id].GroupBy(a => a.QuestionId).ToDictionary(g => g.Key, g => AnswerValue.From(g.First()));
            return QuestionVisibility.IsEligible(scholarship.EligibilityMatch, conditions,
                c => AnswerConditions.Matches(c.OptionId ?? c.SchoolId, c.TextContains, byQuestion.GetValueOrDefault(c.QuestionId)));
        }).ToHashSet();
    }

    // The school picked on the application (first School Select question), else the one on the applicant's account
    private async Task<Dictionary<string, string?>> SchoolNamesAsync(List<CandidateRow> apps, string cycleId)
    {
        var schoolQuestionId = await db.Query("Questions").Where("CycleId", cycleId).Where("Type", "school_select")
            .WhereNull("ReferenceFormId").OrderBy("Order").Select("Id").FirstOrDefaultAsync<string>();
        var picked = schoolQuestionId is null
            ? []
            : (await db.Query("ApplicationAnswers").Where("QuestionId", schoolQuestionId).WhereIn("ApplicationId", apps.Select(a => a.Id))
                .GetAsync<ApplicationAnswer>()).Where(a => !string.IsNullOrEmpty(a.TextValue))
                .GroupBy(a => a.ApplicationId).ToDictionary(g => g.Key, g => g.First().TextValue!);
        var schoolIds = picked.Values.Concat(apps.Select(a => a.SchoolId).OfType<string>()).Distinct().ToList();
        var names = schoolIds.Count == 0
            ? []
            : (await db.Query("Schools").WhereIn("Id", schoolIds).GetAsync<School>()).ToDictionary(s => s.Id, s => s.Name);
        return apps.ToDictionary(a => a.Id, a =>
        {
            var schoolId = picked.GetValueOrDefault(a.Id) ?? a.SchoolId;
            return schoolId is not null && names.TryGetValue(schoolId, out var name) ? name : null;
        });
    }

    private class CandidateRow
    {
        public string Id { get; set; } = null!;
        public string Status { get; set; } = null!;
        public DateTime? SubmittedAt { get; set; }
        public string ApplicantId { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? SchoolId { get; set; }
    }

    private class ScoreAverage
    {
        public string ApplicationId { get; set; } = null!;
        public decimal Average { get; set; }
    }

    private class AwardRow
    {
        public string ApplicationId { get; set; } = null!;
        public decimal? Amount { get; set; }
        public string? Note { get; set; }
        public DateTime AwardedAt { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }
}

public class AwardRequest
{
    public string? ApplicationId { get; set; }
    public decimal? Amount { get; set; }
    public string? Note { get; set; }
}
