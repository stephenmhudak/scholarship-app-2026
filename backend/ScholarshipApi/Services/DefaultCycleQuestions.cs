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
///   - Pages 4–5 (teacher / second reference forms) and the page 2 reference certification
///     (#14) — references are collected through the References workflow, not the applicant.
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
        Rules? Rules = null);

    private record S(string Title, string? Description, Q[] Questions);

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
            new("If you live with one parent, who is responsible for your education?", "single_choice",
                Options: ["Father", "Mother"]),
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
            new("Where are you employed?", "short_answer", Description: "If employed."),
            new("Number of hours worked per week", "short_answer", Rules: Number, Description: "If employed."),
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

    public static async Task SeedAsync(QueryFactory db, string cycleId, IDbTransaction transaction)
    {
        var questionOrder = 0;
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
                var questionId = Guid.NewGuid().ToString();
                await db.Query("Questions").InsertAsync(new
                {
                    Id = questionId,
                    CycleId = cycleId,
                    q.Text,
                    q.Type,
                    Order = questionOrder++,
                    IsRequired = q.Required,
                    SectionId = sectionId,
                    q.Description,
                    ValidationRules = q.Rules != null ? JsonSerializer.Serialize(q.Rules, JsonOptions) : null,
                }, transaction);

                if (q.Options == null) continue;
                for (var o = 0; o < q.Options.Length; o++)
                {
                    await db.Query("QuestionOptions").InsertAsync(new
                    {
                        Id = Guid.NewGuid().ToString(),
                        QuestionId = questionId,
                        Text = q.Options[o],
                        Order = o,
                    }, transaction);
                }
            }
        }
    }
}
