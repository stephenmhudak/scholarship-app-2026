using SqlKata.Execution;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

/// <summary>
/// What a condition (reference requirement, conditional question, scorer filter) can look at in an application
/// answer: a selection (a choice option, or a school for School Select) or, for text questions, some text.
/// Matching is <see cref="AnswerConditions"/>.
/// </summary>
public static class ConditionTarget
{
    public static readonly HashSet<string> ChoiceTypes = ["single_choice", "multiple_choice"];
    public static readonly HashSet<string> TextTypes = ["short_answer", "long_answer"];

    public static bool CanUse(string questionType) =>
        ChoiceTypes.Contains(questionType) || questionType == "school_select" || TextTypes.Contains(questionType);

    /// <summary>Checks what a condition on <paramref name="source"/> looks for and returns it with the unused parts cleared.</summary>
    public static async Task<(string? OptionId, string? SchoolId, string? TextContains)> ValidateAsync(
        QueryFactory db, Question source, string? optionId, string? schoolId, string? textContains)
    {
        if (ChoiceTypes.Contains(source.Type))
        {
            if (!await db.Query("QuestionOptions").Where("Id", optionId ?? "").Where("QuestionId", source.Id).ExistsAsync())
                throw new ArgumentException($"Choose an answer for \"{source.Text}\".");
            return (optionId, null, null);
        }
        if (source.Type == "school_select")
        {
            if (!await db.Query("Schools").Where("Id", schoolId ?? "").ExistsAsync())
                throw new ArgumentException($"Choose a school for \"{source.Text}\".");
            return (null, schoolId, null);
        }
        if (TextTypes.Contains(source.Type))
            return (null, null, InputValidation.Required(textContains, "Text to look for", 255));

        throw new ArgumentException("Conditions can only use choice, school or text questions.");
    }
}
