using System.Data;
using System.Text.Json;
using SqlKata.Execution;

namespace ScholarshipApi.Services;

/// <summary>
/// Default sections and questions seeded into every new scholarship cycle, based on the
/// SFRC paper application (SFRC_Application-20210324-3.pdf). Admins can edit or remove
/// them afterwards from the cycle's question builder.
///
/// Deliberately omitted from the paper form:
///   - The page 2 reference certification (#14). Pages 4–5 (teacher / second reference) become the
///     default reference forms below; referees fill them in through a link, not the applicant.
///   - Duplicate prompts (repeated "Applicant's Name"/"School" headers, page 2's second
///     transcript line).
/// </summary>
public static class DefaultCycleQuestions
{
    private record Rules(
        bool NumberOnly = false,
        bool PhoneFormat = false,
        bool EmailFormat = false,
        int? MinLength = null,
        int? MaxLength = null);

    private record Q(
        string Text,
        string Type,
        bool Required = false,
        string? Description = null,
        string[]? Options = null,
        Rules? Rules = null,
        ShowWhen? ShowWhen = null);

    /// <summary>Only show the question when an earlier question (by text) has one of these options chosen.</summary>
    private record ShowWhen(string Question, params string[] Options);

    private record S(string Title, string? Description, Q[] Questions);

    private record R(string Title, string? Description, Q[] Questions);

    private static readonly Rules Number = new(NumberOnly: true);
    private static readonly Rules Phone = new(PhoneFormat: true);
    private static readonly Rules Email = new(EmailFormat: true);

    private const string SignatureHint = "Type your full legal name to sign electronically.";

    private static readonly S[] Sections =
    [
        new("Applicant Information", null,
        [
            new("Recent close-up color photo", "file_upload", Required: true,
                Description: "Upload a recent close-up color photo of yourself (not a copy)."),
            new("Name of applicant", "short_answer", Required: true),
            new("Mailing address — Street", "short_answer", Required: true),
            new("Mailing address — City", "short_answer", Required: true),
            new("Mailing address — County", "short_answer", Required: true),
            new("Mailing address — Zip", "short_answer", Required: true),
            new("Permanent home address", "long_answer",
                Description: "Leave blank if the same as your mailing address."),
            new("Phone number", "short_answer", Rules: Phone),
            new("Cell number", "short_answer", Required: true, Rules: Phone),
            new("Email address", "short_answer", Required: true, Rules: Email),
            new("Applicant lives with", "single_choice", Required: true,
                Options: ["Both Parents", "Father", "Mother", "Guardian", "Relative"]),
            new("Name of father", "short_answer"),
            new("Name of mother", "short_answer"),
            new("Age of applicant", "short_answer", Required: true, Rules: Number),
            new("Sex", "single_choice", Required: true, Options: ["Male", "Female"]),
        ]),

        new("Academics", null,
        [
            new("High school attended", "school_select", Required: true),
            new("High school city", "short_answer", Required: true),
            new("High school county", "short_answer", Required: true),
            new("Cumulative GPA", "short_answer", Required: true, Rules: Number),
            new("High school transcript", "file_upload", Required: true),
            new("ACT score", "short_answer", Rules: Number,
                Description: "Provide an ACT score, an SAT score, or both."),
            new("SAT score", "short_answer", Rules: Number),
            new("Institution planning to attend", "short_answer", Required: true),
            new("Institution location", "short_answer", Required: true),
            new("Intended major or career path", "short_answer", Required: true),
        ]),

        new("Extracurricular Activities", null,
        [
            new("List clubs or organizations you have participated in along with any offices you held", "long_answer"),
            new("Class officer — Freshman", "short_answer", Description: "Position held, if any."),
            new("Class officer — Sophomore", "short_answer", Description: "Position held, if any."),
            new("Class officer — Junior", "short_answer", Description: "Position held, if any."),
            new("Class officer — Senior", "short_answer", Description: "Position held, if any."),
            new("Other honors and awards", "long_answer"),
            new("Community/church activities", "long_answer"),
            new("List any athletics you have participated in along with any awards received", "long_answer"),
        ]),

        new("Financial Needs", null,
        [
            new("Parents' gross income per year", "short_answer", Required: true, Rules: Number,
                Description: "Whole dollars, numbers only."),
            new("Most recent signed tax return (1040 or 1040A), pages 1 & 2", "file_upload", Required: true,
                Description: "The return on which the applicant is claimed as a dependent."),
            new("Parent's signature", "short_answer", Required: true, Description: "Parent or guardian: " + SignatureHint),
            new("Parent's signature date", "date", Required: true),
            new("If you live with one parent, who is responsible for your education?", "single_choice", Required: true,
                Options: ["Father", "Mother"], ShowWhen: new("Applicant lives with", "Father", "Mother")),
            new("Father's occupation", "short_answer"),
            new("Father's employer", "short_answer"),
            new("Mother's occupation", "short_answer"),
            new("Mother's employer", "short_answer"),
            new("House owned", "short_answer"),
            new("Value of home", "short_answer", Rules: Number, Description: "Whole dollars, numbers only."),
            new("Equity in home", "short_answer", Rules: Number, Description: "Whole dollars, numbers only."),
            new("Acres owned", "short_answer", Rules: Number),
            new("Acres rented", "short_answer", Rules: Number),
            new("Amount of debt", "short_answer", Rules: Number, Description: "Whole dollars, numbers only."),
            new("Dependents in your parents' family, including yourself — names and ages", "long_answer", Required: true,
                Description: "One per line, e.g. \"Jane Doe, 17\"."),
            new("Number in college, excluding yourself", "short_answer", Required: true, Rules: Number),
            new("Unusual circumstances", "long_answer",
                Description: "E.g. illness of a parent, unexpected financial loss, unusually high cost of education such as medical school."),
            new("Are you employed?", "single_choice", Required: true, Options: ["Yes", "No"]),
            new("Where are you employed?", "short_answer", Required: true, ShowWhen: new("Are you employed?", "Yes")),
            new("Number of hours worked per week", "short_answer", Required: true, Rules: Number,
                ShowWhen: new("Are you employed?", "Yes")),
            new("Write a short history of your work experience (include volunteer work)", "long_answer", Required: true),
        ]),

        new("Aspiring Educators", "Only complete this section if you are majoring in education.",
        [
            new("The Stephanie D. Mason Aspiring Educator Memorial Scholarship essay: \"When I meet people for the first time, I hope they walk away from me feeling...\"",
                "long_answer", Description: "One-page response."),
        ]),

        new("Certification", null,
        [
            new("I certify that all information contained in this application is correct.", "multiple_choice", Required: true,
                Options: ["I certify"]),
            new("Applicant signature", "short_answer", Required: true, Description: SignatureHint),
            new("Date", "date", Required: true),
        ]),
    ];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Pages 4–5. The referee sees the applicant's name and school automatically, and every reference ends
    // with the referee's printed name, a drawn signature and the date, so those aren't questions here.
    private static readonly R[] References =
    [
        new("Teacher Reference", "To be completed by a teacher or advisor who knows the applicant.",
        [
            new("How many months were you in frequent contact with the applicant?", "short_answer", Required: true, Rules: Number),
            new("What was your relationship to the applicant?", "single_choice", Required: true,
                Options: ["Teacher", "Advisor", "Other"]),
            new("Course taught, advisory role, or other relationship", "short_answer", Required: true,
                Description: "For example: Teacher for AP Biology."),
            new("Please rate the applicant on overall promise", "single_choice", Required: true,
                Description: "Compared with other students you have known, as a percentile (99 is highest).",
                Options: [.. Enumerable.Range(80, 20).Reverse().Select(p => p.ToString()), "Below 80"]),
            new("Summary comments", "long_answer", Required: true,
                Description: "Can you tell something about this student that is not reflected in grades, test scores, or rank in class? What makes this applicant truly exceptional?"),
        ]),
        new("Second Reference", "From a pastor, employer, civic leader, or anyone else who knows the applicant well.",
        [
            new("Your relationship to the applicant", "short_answer", Required: true,
                Description: "For example: pastor, teacher, employer, civic leader."),
            new("Reference", "long_answer", Required: true,
                Description: "Please share what you know about the applicant and why you recommend them."),
        ]),
    ];

    public static async Task SeedAsync(QueryFactory db, string cycleId, IDbTransaction transaction)
    {
        await SeedApplicationAsync(db, cycleId, transaction);
        await SeedReferencesAsync(db, cycleId, transaction);
    }

    private static async Task SeedReferencesAsync(QueryFactory db, string cycleId, IDbTransaction transaction)
    {
        for (var r = 0; r < References.Length; r++)
        {
            var form = References[r];
            var formId = Guid.NewGuid().ToString();
            await db.Query("ReferenceForms").InsertAsync(new
            {
                Id = formId,
                CycleId = cycleId,
                form.Title,
                form.Description,
                RequiredCount = 1,
                Requirement = "always",
                Order = r,
                CreatedAt = DateTime.UtcNow,
            }, transaction);

            for (var i = 0; i < form.Questions.Length; i++)
                await InsertQuestionAsync(db, transaction, cycleId, form.Questions[i], i, sectionId: null, referenceFormId: formId);
        }
    }

    private static async Task SeedApplicationAsync(QueryFactory db, string cycleId, IDbTransaction transaction)
    {
        var questionOrder = 0;
        // Inserted questions by text, so ShowWhen can find the question (and option ids) it depends on
        var inserted = new Dictionary<string, (string Id, Dictionary<string, string> OptionIds)>();
        for (var s = 0; s < Sections.Length; s++)
        {
            var section = Sections[s];
            var sectionId = Guid.NewGuid().ToString();
            await db.Query("Sections").InsertAsync(new
            {
                Id = sectionId,
                CycleId = cycleId,
                section.Title,
                section.Description,
                Order = s,
            }, transaction);

            foreach (var q in section.Questions)
            {
                var question = await InsertQuestionAsync(db, transaction, cycleId, q, questionOrder++, sectionId, referenceFormId: null);
                inserted[q.Text] = question;
                if (q.ShowWhen is null) continue;

                var source = inserted[q.ShowWhen.Question];
                foreach (var option in q.ShowWhen.Options)
                {
                    await db.Query("QuestionConditions").InsertAsync(new
                    {
                        Id = Guid.NewGuid().ToString(),
                        QuestionId = question.Id,
                        SourceQuestionId = source.Id,
                        OptionId = source.OptionIds[option],
                    }, transaction);
                }
            }
        }
    }

    private static async Task<(string Id, Dictionary<string, string> OptionIds)> InsertQuestionAsync(
        QueryFactory db, IDbTransaction transaction, string cycleId, Q q, int order, string? sectionId, string? referenceFormId)
    {
        var questionId = Guid.NewGuid().ToString();
        await db.Query("Questions").InsertAsync(new
        {
            Id = questionId,
            CycleId = cycleId,
            q.Text,
            q.Type,
            Order = order,
            IsRequired = q.Required,
            SectionId = sectionId,
            ReferenceFormId = referenceFormId,
            q.Description,
            ValidationRules = q.Rules != null ? JsonSerializer.Serialize(q.Rules, JsonOptions) : null,
            Visibility = q.ShowWhen is null ? "always" : "conditional",
        }, transaction);

        var optionIds = new Dictionary<string, string>();
        for (var o = 0; o < (q.Options?.Length ?? 0); o++)
        {
            var optionId = Guid.NewGuid().ToString();
            optionIds[q.Options![o]] = optionId;
            await db.Query("QuestionOptions").InsertAsync(new
            {
                Id = optionId,
                QuestionId = questionId,
                Text = q.Options[o],
                Order = o,
            }, transaction);
        }
        return (questionId, optionIds);
    }
}
