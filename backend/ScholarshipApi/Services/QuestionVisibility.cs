using SqlKata.Execution;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

/// <summary>
/// Which application questions an applicant sees, given their answers (mirrored in frontend/src/utils/questionVisibility.js).
/// A conditional question is shown when any/all of its conditions match, and a condition only counts while its
/// source question is itself shown, so hiding a question also hides the ones that depend on it. A question in a
/// scholarship's own section is also only shown while the applicant is eligible for that scholarship (its gate).
/// </summary>
public static class QuestionVisibility
{
    /// <summary>A conditional scholarship's eligibility rule, applied to the questions in its section.</summary>
    public record Gate(string ScholarshipId, string Match, List<ScholarshipCondition> Conditions);

    public record Rules(ILookup<string, QuestionCondition> Conditions, Dictionary<string, Gate> Gates);

    public static async Task<ILookup<string, QuestionCondition>> LoadConditionsAsync(QueryFactory db, IEnumerable<string> questionIds)
    {
        var ids = questionIds.ToList();
        return ids.Count == 0
            ? Enumerable.Empty<QuestionCondition>().ToLookup(c => c.QuestionId)
            : (await db.Query("QuestionConditions").WhereIn("QuestionId", ids).GetAsync<QuestionCondition>()).ToLookup(c => c.QuestionId);
    }

    /// <summary>Gates by question id, for questions in a conditional scholarship's section (open scholarships have none).</summary>
    public static async Task<Dictionary<string, Gate>> LoadGatesAsync(QueryFactory db, IReadOnlyCollection<Question> questions)
    {
        var sectionIds = questions.Select(q => q.SectionId).OfType<string>().Distinct().ToList();
        if (sectionIds.Count == 0) return [];
        var scholarshipBySection = (await db.Query("Sections").WhereIn("Id", sectionIds).WhereNotNull("ScholarshipId")
            .GetAsync<Section>()).ToDictionary(s => s.Id, s => s.ScholarshipId!);
        if (scholarshipBySection.Count == 0) return [];

        var scholarships = (await db.Query("Scholarships").WhereIn("Id", scholarshipBySection.Values.Distinct())
            .Where("Eligibility", "conditional").GetAsync<Scholarship>()).ToDictionary(s => s.Id);
        var conditions = scholarships.Count == 0
            ? Enumerable.Empty<ScholarshipCondition>().ToLookup(c => c.ScholarshipId)
            : (await db.Query("ScholarshipConditions").WhereIn("ScholarshipId", scholarships.Keys)
                .GetAsync<ScholarshipCondition>()).ToLookup(c => c.ScholarshipId);

        var gates = new Dictionary<string, Gate>();
        foreach (var q in questions)
        {
            if (q.SectionId is null || !scholarshipBySection.TryGetValue(q.SectionId, out var scholarshipId)) continue;
            if (!scholarships.TryGetValue(scholarshipId, out var s)) continue;
            gates[q.Id] = new Gate(s.Id, s.EligibilityMatch, conditions[s.Id].ToList());
        }
        return gates;
    }

    public static async Task<Rules> LoadRulesAsync(QueryFactory db, IReadOnlyCollection<Question> questions) =>
        new(await LoadConditionsAsync(db, questions.Select(q => q.Id)), await LoadGatesAsync(db, questions));

    public static HashSet<string> VisibleIds(
        IReadOnlyCollection<Question> questions, Rules rules, IReadOnlyDictionary<string, AnswerValue> answers)
    {
        var byId = questions.ToDictionary(q => q.Id);
        var memo = new Dictionary<string, bool>();
        var visiting = new HashSet<string>();

        bool SourceMatches(string sourceId, string? selectedId, string? textContains) =>
            IsVisible(sourceId) && AnswerConditions.Matches(selectedId, textContains, answers.GetValueOrDefault(sourceId));

        bool IsVisible(string id)
        {
            if (memo.TryGetValue(id, out var known)) return known;
            if (!byId.TryGetValue(id, out var question)) return false;
            // Saving rejects cycles, but never loop on one
            if (!visiting.Add(id)) return false;

            var visible = true;
            if (question.Visibility == "conditional")
            {
                var own = rules.Conditions[id].ToList();
                bool Matches(QuestionCondition c) => SourceMatches(c.SourceQuestionId, c.OptionId ?? c.SchoolId, c.TextContains);
                // Normally impossible (QuestionOrder makes a conditional question with no conditions always-shown), but stay hidden
                visible = own.Count > 0 && (question.ConditionMatch == "all" ? own.All(Matches) : own.Any(Matches));
            }
            if (visible && rules.Gates.TryGetValue(id, out var gate))
                visible = IsEligible(gate.Match, gate.Conditions, c => SourceMatches(c.QuestionId, c.OptionId ?? c.SchoolId, c.TextContains));

            visiting.Remove(id);
            return memo[id] = visible;
        }

        return questions.Where(q => IsVisible(q.Id)).Select(q => q.Id).ToHashSet();
    }

    /// <summary>A conditional scholarship's rule: any/all of its conditions match (none left means not eligible).</summary>
    public static bool IsEligible(string match, IReadOnlyCollection<ScholarshipCondition> conditions, Func<ScholarshipCondition, bool> matches) =>
        conditions.Count > 0 && (match == "all" ? conditions.All(matches) : conditions.Any(matches));
}
