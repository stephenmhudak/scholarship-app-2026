using System.Data;
using SqlKata.Execution;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

/// <summary>
/// Copies a cycle's sections, questions and options into another cycle, along with which questions are shown
/// conditionally and its reference forms (their questions, and the conditions that tie them to application answers).
/// </summary>
public static class CycleQuestionCopier
{
    public static async Task CopyAsync(QueryFactory db, string sourceCycleId, string targetCycleId, IDbTransaction transaction)
    {
        var sourceExists = await db.Query("ScholarshipCycles").Where("Id", sourceCycleId)
            .ExistsAsync(transaction);
        if (!sourceExists) throw new KeyNotFoundException("Cycle to copy from was not found.");

        var sections = await db.Query("Sections").Where("CycleId", sourceCycleId)
            .GetAsync<Section>(transaction);
        var sectionIdMap = new Dictionary<string, string>();
        foreach (var section in sections)
        {
            var newId = Guid.NewGuid().ToString();
            sectionIdMap[section.Id] = newId;
            await db.Query("Sections").InsertAsync(new
            {
                Id = newId,
                CycleId = targetCycleId,
                section.Title,
                section.Description,
                section.Order,
            }, transaction);
        }

        var forms = await db.Query("ReferenceForms").Where("CycleId", sourceCycleId)
            .GetAsync<ReferenceForm>(transaction);
        var formIdMap = new Dictionary<string, string>();
        foreach (var form in forms)
        {
            var newId = Guid.NewGuid().ToString();
            formIdMap[form.Id] = newId;
            await db.Query("ReferenceForms").InsertAsync(new
            {
                Id = newId,
                CycleId = targetCycleId,
                form.Title,
                form.Description,
                form.RequiredCount,
                form.Requirement,
                form.Order,
                CreatedAt = DateTime.UtcNow,
            }, transaction);
        }

        var questions = (await db.Query("Questions").Where("CycleId", sourceCycleId)
            .GetAsync<Question>(transaction)).ToList();
        if (questions.Count == 0) return;
        var questionIdMap = new Dictionary<string, string>();
        var optionIdMap = new Dictionary<string, string>();

        var options = (await db.Query("QuestionOptions").WhereIn("QuestionId", questions.Select(q => q.Id))
            .GetAsync<QuestionOption>(transaction)).ToLookup(o => o.QuestionId);

        foreach (var question in questions)
        {
            var newId = Guid.NewGuid().ToString();
            questionIdMap[question.Id] = newId;
            await db.Query("Questions").InsertAsync(new
            {
                Id = newId,
                CycleId = targetCycleId,
                question.Text,
                question.Type,
                question.Order,
                question.IsRequired,
                SectionId = question.SectionId != null && sectionIdMap.TryGetValue(question.SectionId, out var s) ? s : null,
                ReferenceFormId = question.ReferenceFormId != null && formIdMap.TryGetValue(question.ReferenceFormId, out var f) ? f : null,
                question.Description,
                question.ValidationRules,
                question.Visibility,
                question.ConditionMatch,
            }, transaction);

            foreach (var option in options[question.Id])
            {
                var newOptionId = Guid.NewGuid().ToString();
                optionIdMap[option.Id] = newOptionId;
                await db.Query("QuestionOptions").InsertAsync(new
                {
                    Id = newOptionId,
                    QuestionId = newId,
                    option.Text,
                    option.Order,
                }, transaction);
            }
        }

        var questionConditions = await db.Query("QuestionConditions").WhereIn("QuestionId", questionIdMap.Keys)
            .GetAsync<QuestionCondition>(transaction);
        foreach (var condition in questionConditions)
        {
            if (!questionIdMap.TryGetValue(condition.SourceQuestionId, out var sourceId)) continue;
            string? optionId = null;
            if (condition.OptionId != null && !optionIdMap.TryGetValue(condition.OptionId, out optionId)) continue;
            await db.Query("QuestionConditions").InsertAsync(new
            {
                Id = Guid.NewGuid().ToString(),
                QuestionId = questionIdMap[condition.QuestionId],
                SourceQuestionId = sourceId,
                OptionId = optionId,
                condition.TextContains,
            }, transaction);
        }

        // Scorers limited to answers on the old cycle's questions get the same limits on the new cycle's questions,
        // so they keep seeing (only) the same kind of application
        var scorerFilters = await db.Query("ScorerFilters").WhereIn("QuestionId", questionIdMap.Keys)
            .GetAsync<ScorerFilter>(transaction);
        foreach (var filter in scorerFilters)
        {
            string? optionId = null;
            if (filter.OptionId != null && !optionIdMap.TryGetValue(filter.OptionId, out optionId)) continue;
            await db.Query("ScorerFilters").InsertAsync(new
            {
                Id = Guid.NewGuid().ToString(),
                filter.ScorerId,
                QuestionId = questionIdMap[filter.QuestionId],
                OptionId = optionId,
                filter.TextContains,
            }, transaction);
        }

        if (formIdMap.Count == 0) return;
        var conditions = await db.Query("ReferenceFormConditions").WhereIn("FormId", formIdMap.Keys)
            .GetAsync<ReferenceFormCondition>(transaction);
        foreach (var condition in conditions)
        {
            if (!questionIdMap.TryGetValue(condition.QuestionId, out var questionId)) continue;
            string? optionId = null;
            if (condition.OptionId != null && !optionIdMap.TryGetValue(condition.OptionId, out optionId)) continue;
            await db.Query("ReferenceFormConditions").InsertAsync(new
            {
                Id = Guid.NewGuid().ToString(),
                FormId = formIdMap[condition.FormId],
                QuestionId = questionId,
                OptionId = optionId,
                condition.TextContains,
            }, transaction);
        }
    }
}
