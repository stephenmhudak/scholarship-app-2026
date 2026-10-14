using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlKata.Execution;
using ScholarshipApi.Models;
using ScholarshipApi.Services;

namespace ScholarshipApi.Controllers;

/// <summary>
/// A cycle's scholarships: name, description, who's eligible, and which accounts can award them. Their questions are
/// managed through <c>/api/cycles/{cycleId}/questions?scholarshipId=…&amp;part=eligibility|details</c>; awarding is
/// <see cref="AwardsController"/>.
/// </summary>
[ApiController]
[Route("api/cycles/{cycleId}/scholarships")]
[Authorize(Policy = "AppAdmin")]
public class ScholarshipsController(QueryFactory db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(string cycleId)
    {
        var scholarships = (await db.Query("Scholarships").Where("CycleId", cycleId)
            .OrderBy("Order", "CreatedAt").GetAsync<Scholarship>()).ToList();
        if (scholarships.Count == 0) return Ok(Array.Empty<object>());

        var ids = scholarships.Select(s => s.Id).ToList();
        var conditions = (await db.Query("ScholarshipConditions").WhereIn("ScholarshipId", ids)
            .GetAsync<ScholarshipCondition>()).ToLookup(c => c.ScholarshipId);
        var awarders = (await db.Query("ScholarshipAwarders as a").Join("Users as u", "u.Id", "a.UserId")
            .WhereIn("a.ScholarshipId", ids)
            .Select("a.ScholarshipId", "u.Id", "u.FirstName", "u.LastName", "u.Email")
            .GetAsync<AwarderRow>()).ToLookup(a => a.ScholarshipId);
        var sectionIds = (await db.Query("Sections").WhereIn("ScholarshipId", ids).GetAsync<Section>())
            .ToDictionary(s => s.ScholarshipId!, s => s.Id);
        var eligibilityCounts = await CountsAsync(db.Query("Questions").WhereIn("ScholarshipId", ids), "ScholarshipId");
        var questionCounts = sectionIds.Count == 0 ? [] : await CountsAsync(db.Query("Questions").WhereIn("SectionId", sectionIds.Values), "SectionId");
        var awardCounts = await CountsAsync(db.Query("ScholarshipAwards").WhereIn("ScholarshipId", ids), "ScholarshipId");

        return Ok(scholarships.Select(s => new
        {
            s.Id,
            s.Name,
            s.Description,
            s.Eligibility,
            s.EligibilityMatch,
            s.AwardAmount,
            Conditions = conditions[s.Id].Select(c => new { c.QuestionId, c.OptionId, c.SchoolId, c.TextContains }),
            Awarders = awarders[s.Id].Select(a => new { a.Id, Name = $"{a.FirstName} {a.LastName}".Trim(), a.Email }),
            EligibilityQuestionCount = eligibilityCounts.GetValueOrDefault(s.Id),
            QuestionCount = sectionIds.TryGetValue(s.Id, out var sectionId) ? questionCounts.GetValueOrDefault(sectionId) : 0,
            AwardCount = awardCounts.GetValueOrDefault(s.Id),
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create(string cycleId, [FromBody] ScholarshipRequest request)
    {
        if (!await db.Query("ScholarshipCycles").Where("Id", cycleId).ExistsAsync()) throw new KeyNotFoundException("Cycle not found.");
        var valid = await ValidateAsync(cycleId, request);

        var id = Guid.NewGuid().ToString();
        await db.Query("Scholarships").InsertAsync(new
        {
            Id = id,
            CycleId = cycleId,
            valid.Name,
            valid.Description,
            valid.Eligibility,
            EligibilityMatch = valid.Match,
            valid.AwardAmount,
            Order = await db.Query("Scholarships").Where("CycleId", cycleId).CountAsync<int>(),
            CreatedAt = DateTime.UtcNow,
        });
        await SaveConditionsAsync(id, valid.Conditions);
        return CreatedAtAction(nameof(List), new { cycleId }, new { id });
    }

    [HttpPut("{scholarshipId}")]
    public async Task<IActionResult> Update(string cycleId, string scholarshipId, [FromBody] ScholarshipRequest request)
    {
        await GetScholarshipAsync(cycleId, scholarshipId);
        var valid = await ValidateAsync(cycleId, request);

        await db.Query("Scholarships").Where("Id", scholarshipId).UpdateAsync(new
        {
            valid.Name,
            valid.Description,
            valid.Eligibility,
            EligibilityMatch = valid.Match,
            valid.AwardAmount,
        });
        await db.Query("ScholarshipConditions").Where("ScholarshipId", scholarshipId).DeleteAsync();
        await SaveConditionsAsync(scholarshipId, valid.Conditions);
        // Its section on the form carries its name and description
        await db.Query("Sections").Where("ScholarshipId", scholarshipId)
            .UpdateAsync(new { Title = valid.Name, valid.Description });
        return NoContent();
    }

    [HttpDelete("{scholarshipId}")]
    public async Task<IActionResult> Delete(string cycleId, string scholarshipId, [FromQuery] bool force = false)
    {
        await GetScholarshipAsync(cycleId, scholarshipId);

        // Its eligibility questions and its own section's questions go with it
        var sectionId = await db.Query("Sections").Where("ScholarshipId", scholarshipId).Select("Id").FirstOrDefaultAsync<string>();
        var questionIds = (await db.Query("Questions")
            .Where(q => q.Where("ScholarshipId", scholarshipId).When(sectionId is not null, w => w.OrWhere("SectionId", sectionId)))
            .Select("Id").GetAsync<string>()).ToList();
        var answerCount = questionIds.Count == 0 ? 0 : await db.Query("ApplicationAnswers").WhereIn("QuestionId", questionIds).CountAsync<int>();
        var awardCount = await db.Query("ScholarshipAwards").Where("ScholarshipId", scholarshipId).CountAsync<int>();
        if ((answerCount > 0 || awardCount > 0) && !force)
            return Conflict(new { error = "This scholarship has answers or awards.", answerCount, awardCount });

        if (db.Connection.State != System.Data.ConnectionState.Open) db.Connection.Open();
        using (var transaction = db.Connection.BeginTransaction())
        {
            // ApplicationAnswers.QuestionId has no ON DELETE rule
            if (questionIds.Count > 0)
            {
                await db.Query("ApplicationAnswers").WhereIn("QuestionId", questionIds).DeleteAsync(transaction);
                await db.Query("Questions").WhereIn("Id", questionIds).DeleteAsync(transaction);
            }
            // Its section, conditions, award accounts and awards cascade
            await db.Query("Scholarships").Where("Id", scholarshipId).DeleteAsync(transaction);

            // No scholarship eligibility questions left: the shared section goes too
            var eligibilitySectionId = await db.Query("Sections").Where("CycleId", cycleId)
                .Where("Kind", ScholarshipSections.EligibilityKind).Select("Id").FirstOrDefaultAsync<string>(transaction);
            if (eligibilitySectionId is not null
                && !await db.Query("Questions").Where("SectionId", eligibilitySectionId).ExistsAsync(transaction))
                await db.Query("Sections").Where("Id", eligibilitySectionId).DeleteAsync(transaction);

            transaction.Commit();
        }
        return Ok(await QuestionOrder.PruneConditionsAsync(db, cycleId));
    }

    // Who can award it. Applicants and reference providers can't; super admins always can.
    [HttpPut("{scholarshipId}/awarders")]
    public async Task<IActionResult> SetAwarders(string cycleId, string scholarshipId, [FromBody] AwardersRequest request)
    {
        await GetScholarshipAsync(cycleId, scholarshipId);
        var userIds = (request.UserIds ?? []).Distinct().ToList();
        if (userIds.Count > 0)
        {
            var roles = (await db.Query("Users").WhereIn("Id", userIds).Select("Role").GetAsync<string>()).ToList();
            if (roles.Count != userIds.Count) throw new ArgumentException("One of those accounts doesn't exist.");
            if (roles.Any(r => r is "applicant" or "referee"))
                throw new ArgumentException("Applicants and reference providers can't award scholarships.");
        }

        if (db.Connection.State != System.Data.ConnectionState.Open) db.Connection.Open();
        using var transaction = db.Connection.BeginTransaction();
        await db.Query("ScholarshipAwarders").Where("ScholarshipId", scholarshipId).DeleteAsync(transaction);
        foreach (var userId in userIds)
            await db.Query("ScholarshipAwarders").InsertAsync(new { ScholarshipId = scholarshipId, UserId = userId }, transaction);
        transaction.Commit();
        return NoContent();
    }

    private record ValidScholarship(string Name, string? Description, decimal? AwardAmount, string Eligibility, string Match,
        List<ScholarshipConditionRequest> Conditions);

    private async Task<ValidScholarship> ValidateAsync(string cycleId, ScholarshipRequest request)
    {
        var name = InputValidation.Required(request.Name, "Name", 255);
        var description = InputValidation.Optional(request.Description, "Description", 5000);
        if (request.AwardAmount is < 0 or > 1_000_000_000m) throw new ArgumentException("Enter a valid award amount.");
        var awardAmount = request.AwardAmount is { } amount ? Math.Round(amount, 2) : (decimal?)null;
        var eligibility = request.Eligibility ?? "open";
        var match = request.EligibilityMatch ?? "any";
        if (eligibility is not ("open" or "conditional")) throw new ArgumentException("Choose who's eligible.");
        if (match is not ("any" or "all")) throw new ArgumentException("Choose whether any or all conditions must match.");

        var conditions = eligibility == "conditional" ? request.Conditions ?? [] : [];
        if (eligibility == "conditional" && conditions.Count == 0)
            throw new ArgumentException("Add at least one answer that makes applicants eligible, or open it to everyone.");

        foreach (var condition in conditions)
        {
            var question = await db.Query("Questions").Where("Id", condition.QuestionId ?? "").Where("CycleId", cycleId)
                .WhereNull("ReferenceFormId").FirstOrDefaultAsync<Question>()
                ?? throw new ArgumentException("A condition uses a question that isn't on this cycle's application.");
            // A scholarship's own questions only show once someone is eligible, so they can't decide eligibility
            if (question.SectionId is not null
                && await db.Query("Sections").Where("Id", question.SectionId).WhereNotNull("ScholarshipId").ExistsAsync())
                throw new ArgumentException($"\"{question.Text}\" is a scholarship's own question, so it can't decide eligibility. Use an eligibility question.");
            (condition.OptionId, condition.SchoolId, condition.TextContains) =
                await ConditionTarget.ValidateAsync(db, question, condition.OptionId, condition.SchoolId, condition.TextContains);
        }

        return new ValidScholarship(name, description, awardAmount, eligibility, match, conditions);
    }

    private async Task SaveConditionsAsync(string scholarshipId, List<ScholarshipConditionRequest> conditions)
    {
        foreach (var c in conditions)
        {
            await db.Query("ScholarshipConditions").InsertAsync(new
            {
                Id = Guid.NewGuid().ToString(),
                ScholarshipId = scholarshipId,
                c.QuestionId,
                c.OptionId,
                c.SchoolId,
                c.TextContains,
            });
        }
    }

    private async Task<Scholarship> GetScholarshipAsync(string cycleId, string scholarshipId) =>
        await db.Query("Scholarships").Where("Id", scholarshipId).Where("CycleId", cycleId).FirstOrDefaultAsync<Scholarship>()
            ?? throw new KeyNotFoundException("Scholarship not found.");

    private static async Task<Dictionary<string, int>> CountsAsync(SqlKata.Query query, string column) =>
        (await query.GroupBy(column).SelectRaw($"{column} AS GroupId, COUNT(*) AS Total").GetAsync<GroupCount>())
            .ToDictionary(r => r.GroupId, r => r.Total);

    private class GroupCount
    {
        public string GroupId { get; set; } = null!;
        public int Total { get; set; }
    }

    private class AwarderRow
    {
        public string ScholarshipId { get; set; } = null!;
        public string Id { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string Email { get; set; } = null!;
    }
}

public class ScholarshipRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    /// <summary>What it's worth: pre-fills awards and fills {{award_amount}} in letters.</summary>
    public decimal? AwardAmount { get; set; }
    /// <summary><c>open</c> (everyone) or <c>conditional</c> (any/all of <see cref="Conditions"/> match).</summary>
    public string? Eligibility { get; set; }
    public string? EligibilityMatch { get; set; }
    public List<ScholarshipConditionRequest>? Conditions { get; set; }
}

public class ScholarshipConditionRequest
{
    public string? QuestionId { get; set; }
    public string? OptionId { get; set; }
    public string? SchoolId { get; set; }
    public string? TextContains { get; set; }
}

public class AwardersRequest
{
    public List<string>? UserIds { get; set; }
}
