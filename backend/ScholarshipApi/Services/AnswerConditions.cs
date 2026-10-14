using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ScholarshipApi.DTOs.Application;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

/// <summary>An answer as either a saved row or a draft being saved: text (incl. single choice option id) or selected option ids.</summary>
public record AnswerValue(string? Text, IReadOnlyCollection<string>? Selected)
{
    public static AnswerValue From(ApplicationAnswer a) =>
        new(a.TextValue, a.SelectedOptions is null ? null : JsonSerializer.Deserialize<List<string>>(a.SelectedOptions));

    public static AnswerValue From(AnswerDto a) => new(a.TextValue, a.SelectedOptions);
}

/// <summary>
/// The test behind conditional questions and conditional references (mirrored in frontend/src/utils/questionVisibility.js):
/// a choice answer includes <c>optionId</c>, or a text answer contains <c>textContains</c>, ignoring case.
/// </summary>
public static partial class AnswerConditions
{
    public static bool Matches(string? optionId, string? textContains, AnswerValue? answer)
    {
        if (answer is null) return false;

        if (optionId is not null)
            return answer.Text == optionId || (answer.Selected?.Contains(optionId) ?? false);

        if (string.IsNullOrWhiteSpace(textContains) || string.IsNullOrEmpty(answer.Text)) return false;
        // Rich text answers are HTML; match the visible text
        var text = WebUtility.HtmlDecode(Tags().Replace(answer.Text, " "));
        return text.Contains(textContains.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Tags();
}
