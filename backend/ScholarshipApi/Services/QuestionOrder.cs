using System.Data;
using SqlKata.Execution;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

/// <summary>
/// Keeps conditional questions pointing backwards: a question can only depend on questions that come before it on
/// the form. After anything that reorders or removes questions, <see cref="PruneConditionsAsync"/> removes conditions
/// whose source now comes after (or at) the question, and turns conditional questions left with no conditions back
/// into ones every applicant sees.
/// </summary>
public static class QuestionOrder
{
    /// <summary>What a prune changed, by question text, for the admin to see.</summary>
    public record PruneResult(List<string> ConditionsRemovedFrom, List<string> NowAlwaysShown);

    /// <summary>
    /// Each application question's position on the form, as the applicant sees it: sections in order, ungrouped last,
    /// then by question order (same as CycleQuestionsPage/ApplicationFormPage). <paramref name="adjust"/> can override
    /// a question's section and order to check a change before saving it.
    /// </summary>
    public static Dictionary<string, int> Positions(
        IEnumerable<Question> questions, IEnumerable<Section> sections, Func<Question, (string? SectionId, int Order)>? adjust = null)
    {
        var sectionRank = sections.OrderBy(s => s.Order).ThenBy(s => s.Id, StringComparer.Ordinal)
            .Select((s, i) => (s.Id, i)).ToDictionary(x => x.Id, x => x.i);
        return questions
            .Select(q => (q.Id, Place: adjust?.Invoke(q) ?? (q.SectionId, q.Order)))
            .OrderBy(x => x.Place.SectionId is not null && sectionRank.TryGetValue(x.Place.SectionId, out var rank) ? rank : int.MaxValue)
            .ThenBy(x => x.Place.Order)
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .Select((x, i) => (x.Id, i))
            .ToDictionary(x => x.Id, x => x.i);
    }

    public static async Task<PruneResult> PruneConditionsAsync(QueryFactory db, string cycleId, IDbTransaction? transaction = null)
    {
        var questions = (await db.Query("Questions").Where("CycleId", cycleId).WhereNull("ReferenceFormId")
            .GetAsync<Question>(transaction)).ToList();
        var sections = await db.Query("Sections").Where("CycleId", cycleId).GetAsync<Section>(transaction);
        var position = Positions(questions, sections);
        var conditions = questions.Count == 0
            ? []
            : (await db.Query("QuestionConditions").WhereIn("QuestionId", questions.Select(q => q.Id))
                .GetAsync<QuestionCondition>(transaction)).ToList();

        // Sources that now come after the question (deleted sources are already gone, by ON DELETE CASCADE)
        var outOfOrder = conditions
            .Where(c => !position.TryGetValue(c.SourceQuestionId, out var source) || source >= position[c.QuestionId])
            .ToList();
        if (outOfOrder.Count > 0)
            await db.Query("QuestionConditions").WhereIn("Id", outOfOrder.Select(c => c.Id)).DeleteAsync(transaction);

        // Conditional questions with nothing left to match are shown to everyone
        var stillConditioned = conditions.Except(outOfOrder).Select(c => c.QuestionId).ToHashSet();
        var orphaned = questions.Where(q => q.Visibility == "conditional" && !stillConditioned.Contains(q.Id)).ToList();
        if (orphaned.Count > 0)
            await db.Query("Questions").WhereIn("Id", orphaned.Select(q => q.Id)).UpdateAsync(new { Visibility = "always" }, transaction);

        var byId = questions.ToDictionary(q => q.Id);
        return new PruneResult(
            outOfOrder.Select(c => c.QuestionId).Distinct().Select(id => byId[id].Text).ToList(),
            orphaned.Select(q => q.Text).ToList());
    }
}
