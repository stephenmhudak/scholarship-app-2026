using System.Data;
using SqlKata.Execution;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

/// <summary>
/// Copies a cycle's sections, questions and options into another cycle.
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

        var questions = (await db.Query("Questions").Where("CycleId", sourceCycleId)
            .GetAsync<Question>(transaction)).ToList();
        if (questions.Count == 0) return;

        var options = (await db.Query("QuestionOptions").WhereIn("QuestionId", questions.Select(q => q.Id))
            .GetAsync<QuestionOption>(transaction)).ToLookup(o => o.QuestionId);

        foreach (var question in questions)
        {
            var newId = Guid.NewGuid().ToString();
            await db.Query("Questions").InsertAsync(new
            {
                Id = newId,
                CycleId = targetCycleId,
                question.Text,
                question.Type,
                question.Order,
                question.IsRequired,
                SectionId = question.SectionId != null && sectionIdMap.TryGetValue(question.SectionId, out var s) ? s : null,
                question.Description,
                question.ValidationRules,
            }, transaction);

            foreach (var option in options[question.Id])
            {
                await db.Query("QuestionOptions").InsertAsync(new
                {
                    Id = Guid.NewGuid().ToString(),
                    QuestionId = newId,
                    option.Text,
                    option.Order,
                }, transaction);
            }
        }
    }
}
