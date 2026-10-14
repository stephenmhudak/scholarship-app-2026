using SqlKata.Execution;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

/// <summary>
/// Which application questions an applicant sees, given their answers (mirrored in frontend/src/utils/questionVisibility.js).
/// A conditional question is shown when any/all of its conditions match, and a condition only counts while its
/// source question is itself shown, so hiding a question also hides the ones that depend on it.
/// </summary>
public static class QuestionVisibility
{
    public static async Task<ILookup<string, QuestionCondition>> LoadConditionsAsync(QueryFactory db, IEnumerable<string> questionIds)
    {
        var ids = questionIds.ToList();
        return ids.Count == 0
            ? Enumerable.Empty<QuestionCondition>().ToLookup(c => c.QuestionId)
            : (await db.Query("QuestionConditions").WhereIn("QuestionId", ids).GetAsync<QuestionCondition>()).ToLookup(c => c.QuestionId);
    }

    public static HashSet<string> VisibleIds(
        IReadOnlyCollection<Question> questions,
        ILookup<string, QuestionCondition> conditions,
        IReadOnlyDictionary<string, AnswerValue> answers)
    {
        var byId = questions.ToDictionary(q => q.Id);
        var memo = new Dictionary<string, bool>();
        var visiting = new HashSet<string>();

        bool IsVisible(string id)
        {
            if (memo.TryGetValue(id, out var known)) return known;
            if (!byId.TryGetValue(id, out var question)) return false;
            if (question.Visibility != "conditional") return memo[id] = true;
            // Saving rejects cycles, but never loop on one
            if (!visiting.Add(id)) return false;

            var own = conditions[id].ToList();
            bool Matches(QuestionCondition c) =>
                IsVisible(c.SourceQuestionId)
                && AnswerConditions.Matches(c.OptionId, c.TextContains, answers.GetValueOrDefault(c.SourceQuestionId));
            // Normally impossible (QuestionOrder makes a conditional question with no conditions always-shown), but stay hidden
            var visible = own.Count > 0 && (question.ConditionMatch == "all" ? own.All(Matches) : own.Any(Matches));

            visiting.Remove(id);
            return memo[id] = visible;
        }

        return questions.Where(q => IsVisible(q.Id)).Select(q => q.Id).ToHashSet();
    }
}
