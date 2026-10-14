using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using SqlKata.Execution;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

/// <summary>
/// Fills an award letter's merge fields. Built-in fields are <c>{{awardee_name}}</c> etc. (<see cref="Fields"/>);
/// <c>{{answer:Question text}}</c> is the awardee's answer to that application question (matched by text, ignoring case),
/// e.g. their mailing address. Every value is HTML-encoded, since letter bodies are HTML.
/// </summary>
public static partial class LetterMerge
{
    public record Field(string Token, string Label);

    public static readonly Field[] Fields =
    [
        new("awardee_name", "Awardee's full name"),
        new("awardee_first_name", "Awardee's first name"),
        new("awardee_last_name", "Awardee's last name"),
        new("awardee_email", "Awardee's email"),
        new("school_name", "Awardee's school"),
        new("scholarship_name", "Scholarship name"),
        new("scholarship_description", "Scholarship description"),
        new("award_amount", "Award amount"),
        new("award_note", "Award note"),
        new("cycle_name", "Cycle name"),
        new("date", "Today's date"),
    ];

    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Values for one award: built-in fields plus answers by lower-cased question text.</summary>
    public record Values(Dictionary<string, string> Fields, Dictionary<string, string> Answers);

    [GeneratedRegex(@"\{\{\s*(answer:\s*(?<question>[^}]+?)|(?<field>[a-z_]+))\s*\}\}", RegexOptions.IgnoreCase)]
    private static partial Regex Token();

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Tags();

    /// <summary>The letter with its fields filled in. Unknown fields and missing answers come out empty.</summary>
    public static string Apply(string body, Values values) =>
        Token().Replace(body, m =>
        {
            var raw = m.Groups["question"].Success
                ? values.Answers.GetValueOrDefault(m.Groups["question"].Value.Trim().ToLowerInvariant(), "")
                : values.Fields.GetValueOrDefault(m.Groups["field"].Value.ToLowerInvariant(), "");
            // Multi-line answers (an address) keep their lines
            return WebUtility.HtmlEncode(raw).Replace("\r\n", "\n").Replace("\n", "<br>");
        });

    public static string Money(decimal? amount) => amount is { } a ? a.ToString("C", Us) : "";

    /// <summary>The values for an awarded application.</summary>
    public static async Task<Values> ForAwardAsync(QueryFactory db, Scholarship scholarship, ScholarshipAward award)
    {
        var app = await db.Query("Applications").Where("Id", award.ApplicationId).FirstAsync<Application>();
        var applicant = await db.Query("Users").Where("Id", app.ApplicantId).FirstAsync<User>();
        var cycle = await db.Query("ScholarshipCycles").Where("Id", scholarship.CycleId).FirstAsync<ScholarshipCycle>();

        var questions = (await db.Query("Questions").Where("CycleId", scholarship.CycleId).WhereNull("ReferenceFormId")
            .GetAsync<Question>()).ToList();
        var options = questions.Count == 0 ? [] : (await db.Query("QuestionOptions").WhereIn("QuestionId", questions.Select(q => q.Id))
            .GetAsync<QuestionOption>()).ToDictionary(o => o.Id, o => o.Text);
        var answers = (await db.Query("ApplicationAnswers").Where("ApplicationId", app.Id).GetAsync<ApplicationAnswer>())
            .GroupBy(a => a.QuestionId).ToDictionary(g => g.Key, g => g.First());
        var schoolIds = answers.Values.Where(a => questions.Any(q => q.Id == a.QuestionId && q.Type == "school_select"))
            .Select(a => a.TextValue).OfType<string>().Append(applicant.SchoolId).OfType<string>().Distinct().ToList();
        var schools = schoolIds.Count == 0 ? [] : (await db.Query("Schools").WhereIn("Id", schoolIds).GetAsync<School>())
            .ToDictionary(s => s.Id, s => s.Name);

        var answerTexts = new Dictionary<string, string>();
        foreach (var q in questions)
        {
            var key = q.Text.Trim().ToLowerInvariant();
            if (answerTexts.ContainsKey(key)) continue;
            answerTexts[key] = answers.TryGetValue(q.Id, out var a) ? AnswerText(q, a, options, schools) : "";
        }

        // Their school: the first School Select answer, else the school on their account
        var schoolQuestion = questions.Where(q => q.Type == "school_select").OrderBy(q => q.Order).FirstOrDefault();
        var schoolId = schoolQuestion is not null && answers.TryGetValue(schoolQuestion.Id, out var sa) && !string.IsNullOrEmpty(sa.TextValue)
            ? sa.TextValue : applicant.SchoolId;

        return new Values(new Dictionary<string, string>
        {
            ["awardee_name"] = $"{applicant.FirstName} {applicant.LastName}".Trim(),
            ["awardee_first_name"] = applicant.FirstName,
            ["awardee_last_name"] = applicant.LastName,
            ["awardee_email"] = applicant.Email,
            ["school_name"] = schoolId is not null ? schools.GetValueOrDefault(schoolId, "") : "",
            ["scholarship_name"] = scholarship.Name,
            ["scholarship_description"] = scholarship.Description ?? "",
            ["award_amount"] = Money(award.Amount ?? scholarship.AwardAmount),
            ["award_note"] = award.Note ?? "",
            ["cycle_name"] = cycle.Name,
            ["date"] = DateTime.UtcNow.ToString("MMMM d, yyyy", Us),
        }, answerTexts);
    }

    /// <summary>Sample values for previewing a letter with no award behind it; answers show as "[question text]".</summary>
    public static async Task<Values> SampleAsync(QueryFactory db, string cycleId, Scholarship? scholarship)
    {
        var cycle = await db.Query("ScholarshipCycles").Where("Id", cycleId).FirstAsync<ScholarshipCycle>();
        var questions = await db.Query("Questions").Where("CycleId", cycleId).WhereNull("ReferenceFormId").Select("Text").GetAsync<string>();
        return new Values(new Dictionary<string, string>
        {
            ["awardee_name"] = "Jordan Sample",
            ["awardee_first_name"] = "Jordan",
            ["awardee_last_name"] = "Sample",
            ["awardee_email"] = "jordan.sample@example.com",
            ["school_name"] = "Sample High School",
            ["scholarship_name"] = scholarship?.Name ?? "Sample Scholarship",
            ["scholarship_description"] = scholarship?.Description ?? "A description of the scholarship.",
            ["award_amount"] = Money(scholarship?.AwardAmount ?? 1000m),
            ["award_note"] = "A note about this award.",
            ["cycle_name"] = cycle.Name,
            ["date"] = DateTime.UtcNow.ToString("MMMM d, yyyy", Us),
        }, questions.Select(t => t.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(t => t.ToLowerInvariant(), t => $"[{t}]"));
    }

    // An answer as letter text: option and school names, long dates, rich text without its markup; no file ids
    private static string AnswerText(Question q, ApplicationAnswer a, Dictionary<string, string> options, Dictionary<string, string> schools)
    {
        switch (q.Type)
        {
            case "single_choice":
                return a.TextValue is not null ? options.GetValueOrDefault(a.TextValue, "") : "";
            case "multiple_choice":
                return string.Join(", ", (JsonSerializer.Deserialize<List<string>>(a.SelectedOptions ?? "[]") ?? [])
                    .Select(id => options.GetValueOrDefault(id)).OfType<string>());
            case "school_select":
                return a.TextValue is not null ? schools.GetValueOrDefault(a.TextValue, "") : "";
            case "date":
                return DateOnly.TryParseExact(a.TextValue, "yyyy-MM-dd", out var d) ? d.ToString("MMMM d, yyyy", Us) : a.TextValue ?? "";
            case "file_upload":
                return "";
            default:
                return QuestionRules.IsRichText(q) && a.TextValue is not null
                    ? WebUtility.HtmlDecode(Tags().Replace(a.TextValue.Replace("</p>", "\n"), "")).Trim()
                    : a.TextValue ?? "";
        }
    }
}
