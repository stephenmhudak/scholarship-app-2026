using SqlKata.Execution;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

/// <summary>
/// Which of a cycle's reference forms an application needs, based on its saved answers. A form is required when its
/// <c>Requirement</c> is <c>always</c>, or when it's <c>conditional</c> and any one of its conditions matches.
/// </summary>
public static class ReferenceRequirements
{
    public record FormRequirement(ReferenceForm Form, bool Required);

    public static async Task<List<FormRequirement>> EvaluateAsync(QueryFactory db, string applicationId, string cycleId)
    {
        var forms = (await db.Query("ReferenceForms").Where("CycleId", cycleId)
            .OrderBy("Order").OrderBy("CreatedAt").GetAsync<ReferenceForm>()).ToList();
        if (forms.Count == 0) return [];

        var conditions = (await db.Query("ReferenceFormConditions").WhereIn("FormId", forms.Select(f => f.Id))
            .GetAsync<ReferenceFormCondition>()).ToLookup(c => c.FormId);
        var answers = (await db.Query("ApplicationAnswers").Where("ApplicationId", applicationId)
            .GetAsync<ApplicationAnswer>()).GroupBy(a => a.QuestionId).ToDictionary(g => g.Key, g => g.First());

        return forms.Select(f => new FormRequirement(f,
            f.Requirement == "always" || conditions[f.Id].Any(c => Matches(c, answers)))).ToList();
    }

    /// <summary>Required forms that don't yet have enough submitted references, as "Title (1 of 2 received)".</summary>
    public static async Task<List<string>> MissingAsync(QueryFactory db, string applicationId, string cycleId)
    {
        var required = (await EvaluateAsync(db, applicationId, cycleId)).Where(r => r.Required).ToList();
        if (required.Count == 0) return [];

        var received = (await db.Query("ReferenceRequests").Where("ApplicationId", applicationId).Where("Status", "submitted")
            .Select("FormId").GetAsync<string>()).GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
        return required
            .Select(r => (r.Form, Count: received.GetValueOrDefault(r.Form.Id)))
            .Where(r => r.Count < r.Form.RequiredCount)
            .Select(r => $"{r.Form.Title} ({r.Count} of {r.Form.RequiredCount} received)")
            .ToList();
    }

    private static bool Matches(ReferenceFormCondition condition, Dictionary<string, ApplicationAnswer> answers) =>
        answers.TryGetValue(condition.QuestionId, out var answer)
        && AnswerConditions.Matches(condition.OptionId ?? condition.SchoolId, condition.TextContains, AnswerValue.From(answer));
}
