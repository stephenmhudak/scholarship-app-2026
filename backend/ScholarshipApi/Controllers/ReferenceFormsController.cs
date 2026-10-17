using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlKata.Execution;
using ScholarshipApi.Models;
using ScholarshipApi.Services;

namespace ScholarshipApi.Controllers;

/// <summary>
/// A cycle's reference forms. Each one's questions are managed through
/// <c>/api/cycles/{cycleId}/questions?referenceFormId=…</c>.
/// </summary>
[ApiController]
[Route("api/cycles/{cycleId}/reference-forms")]
[Authorize(Policy = "AppAdmin")]
public class ReferenceFormsController(QueryFactory db) : ControllerBase
{
    private const int MaxRequiredCount = 10;

    [HttpGet]
    public async Task<IActionResult> List(string cycleId)
    {
        var forms = (await db.Query("ReferenceForms").Where("CycleId", cycleId)
            .OrderBy("Order").OrderBy("CreatedAt").GetAsync<ReferenceForm>()).ToList();
        if (forms.Count == 0) return Ok(Array.Empty<object>());

        var formIds = forms.Select(f => f.Id).ToList();
        var conditions = (await db.Query("ReferenceFormConditions").WhereIn("FormId", formIds)
            .GetAsync<ReferenceFormCondition>()).ToLookup(c => c.FormId);
        // Typed rows: with dynamic ones the dictionary's values are dynamic, so a form with no questions came out null, not 0
        var questionCounts = (await db.Query("Questions").WhereIn("ReferenceFormId", formIds)
            .GroupBy("ReferenceFormId").SelectRaw("ReferenceFormId AS FormId, COUNT(*) AS Total").GetAsync<FormQuestionCount>())
            .ToDictionary(r => r.FormId, r => r.Total);

        return Ok(forms.Select(f => new
        {
            f.Id,
            f.Title,
            f.Description,
            f.RequiredCount,
            f.Requirement,
            f.Order,
            QuestionCount = questionCounts.GetValueOrDefault(f.Id),
            Conditions = conditions[f.Id].Select(c => new { c.QuestionId, c.OptionId, c.SchoolId, c.TextContains }),
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create(string cycleId, [FromBody] ReferenceFormRequest request)
    {
        if (!await db.Query("ScholarshipCycles").Where("Id", cycleId).ExistsAsync()) throw new KeyNotFoundException("Application period not found.");
        var form = await ValidateAsync(cycleId, request);

        var id = Guid.NewGuid().ToString();
        var order = await db.Query("ReferenceForms").Where("CycleId", cycleId).CountAsync<int>();
        using var transaction = BeginTransaction();
        await db.Query("ReferenceForms").InsertAsync(new
        {
            Id = id,
            CycleId = cycleId,
            form.Title,
            form.Description,
            form.RequiredCount,
            form.Requirement,
            Order = order,
            CreatedAt = DateTime.UtcNow,
        }, transaction);
        await SaveConditionsAsync(id, form.Conditions, transaction);
        transaction.Commit();

        return CreatedAtAction(nameof(List), new { cycleId }, new { id });
    }

    [HttpPut("{formId}")]
    public async Task<IActionResult> Update(string cycleId, string formId, [FromBody] ReferenceFormRequest request)
    {
        if (!await db.Query("ReferenceForms").Where("Id", formId).Where("CycleId", cycleId).ExistsAsync())
            throw new KeyNotFoundException("Reference form not found.");
        var form = await ValidateAsync(cycleId, request);

        using var transaction = BeginTransaction();
        await db.Query("ReferenceForms").Where("Id", formId).UpdateAsync(new
        {
            form.Title,
            form.Description,
            form.RequiredCount,
            form.Requirement,
        }, transaction);
        await db.Query("ReferenceFormConditions").Where("FormId", formId).DeleteAsync(transaction);
        await SaveConditionsAsync(formId, form.Conditions, transaction);
        transaction.Commit();

        return NoContent();
    }

    [HttpDelete("{formId}")]
    public async Task<IActionResult> Delete(string cycleId, string formId, [FromQuery] bool force = false)
    {
        if (!await db.Query("ReferenceForms").Where("Id", formId).Where("CycleId", cycleId).ExistsAsync())
            throw new KeyNotFoundException("Reference form not found.");

        // Its questions, links, submitted references and their scores go with it (ON DELETE CASCADE)
        var requestCount = await db.Query("ReferenceRequests").Where("FormId", formId).CountAsync<int>();
        if (requestCount > 0 && !force)
            return Conflict(new { error = $"{requestCount} reference link(s) use this form.", requestCount });

        await db.Query("ReferenceForms").Where("Id", formId).DeleteAsync();
        return NoContent();
    }

    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder(string cycleId, [FromBody] List<ReorderItem> items)
    {
        foreach (var item in items)
            await db.Query("ReferenceForms").Where("Id", item.Id).Where("CycleId", cycleId).UpdateAsync(new { item.Order });
        return NoContent();
    }

    private record ValidForm(string Title, string? Description, int RequiredCount, string Requirement, List<ReferenceConditionRequest> Conditions);

    private async Task<ValidForm> ValidateAsync(string cycleId, ReferenceFormRequest request)
    {
        var title = InputValidation.Required(request.Title, "Title", 255);
        var description = InputValidation.Optional(request.Description, "Description", 5000);
        if (request.RequiredCount is < 1 or > MaxRequiredCount)
            throw new ArgumentException($"Number required must be between 1 and {MaxRequiredCount}.");
        if (request.Requirement is not ("always" or "conditional"))
            throw new ArgumentException("Choose when this reference is required.");

        var conditions = request.Requirement == "conditional" ? request.Conditions ?? [] : [];
        if (request.Requirement == "conditional" && conditions.Count == 0)
            throw new ArgumentException("Add at least one answer that requires this reference, or make it always required.");

        foreach (var condition in conditions)
        {
            var question = await db.Query("Questions").Where("Id", condition.QuestionId ?? "").Where("CycleId", cycleId)
                .WhereNull("ReferenceFormId").FirstOrDefaultAsync<Question>()
                ?? throw new ArgumentException("A condition uses a question that isn't on this application period's form.");

            (condition.OptionId, condition.SchoolId, condition.TextContains) =
                await ConditionTarget.ValidateAsync(db, question, condition.OptionId, condition.SchoolId, condition.TextContains);
        }

        return new ValidForm(title, description, request.RequiredCount, request.Requirement, conditions);
    }

    private async Task SaveConditionsAsync(string formId, List<ReferenceConditionRequest> conditions, IDbTransaction transaction)
    {
        foreach (var c in conditions)
        {
            await db.Query("ReferenceFormConditions").InsertAsync(new
            {
                Id = Guid.NewGuid().ToString(),
                FormId = formId,
                c.QuestionId,
                c.OptionId,
                c.SchoolId,
                c.TextContains,
            }, transaction);
        }
    }

    private IDbTransaction BeginTransaction()
    {
        if (db.Connection.State != ConnectionState.Open) db.Connection.Open();
        return db.Connection.BeginTransaction();
    }
}

internal class FormQuestionCount
{
    public string FormId { get; set; } = null!;
    public int Total { get; set; }
}

public class ReferenceFormRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public int RequiredCount { get; set; } = 1;
    /// <summary><c>always</c>, or <c>conditional</c>: required when any of <see cref="Conditions"/> matches.</summary>
    public string? Requirement { get; set; }
    public List<ReferenceConditionRequest>? Conditions { get; set; }
}

public class ReferenceConditionRequest
{
    public string? QuestionId { get; set; }
    /// <summary>For choice questions: required when this option is chosen.</summary>
    public string? OptionId { get; set; }
    /// <summary>For text questions: required when the answer contains this text (ignoring case).</summary>
    /// <summary>For School Select questions: the school chosen.</summary>
    public string? SchoolId { get; set; }
    public string? TextContains { get; set; }
}
