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
    private static readonly HashSet<string> ChoiceTypes = ["single_choice", "multiple_choice"];
    private static readonly HashSet<string> TextTypes = ["short_answer", "long_answer"];

    [HttpGet]
    public async Task<IActionResult> List(string cycleId)
    {
        var forms = (await db.Query("ReferenceForms").Where("CycleId", cycleId)
            .OrderBy("Order").OrderBy("CreatedAt").GetAsync<ReferenceForm>()).ToList();
        if (forms.Count == 0) return Ok(Array.Empty<object>());

        var formIds = forms.Select(f => f.Id).ToList();
        var conditions = (await db.Query("ReferenceFormConditions").WhereIn("FormId", formIds)
            .GetAsync<ReferenceFormCondition>()).ToLookup(c => c.FormId);
        var questionCounts = (await db.Query("Questions").WhereIn("ReferenceFormId", formIds)
            .GroupBy("ReferenceFormId").SelectRaw("ReferenceFormId, COUNT(*) AS Total").GetAsync<dynamic>())
            .ToDictionary(r => (string)r.ReferenceFormId, r => Convert.ToInt32(r.Total));

        return Ok(forms.Select(f => new
        {
            f.Id,
            f.Title,
            f.Description,
            f.RequiredCount,
            f.Requirement,
            f.Order,
            QuestionCount = questionCounts.GetValueOrDefault(f.Id),
            Conditions = conditions[f.Id].Select(c => new { c.QuestionId, c.OptionId, c.TextContains }),
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create(string cycleId, [FromBody] ReferenceFormRequest request)
    {
        if (!await db.Query("ScholarshipCycles").Where("Id", cycleId).ExistsAsync()) throw new KeyNotFoundException("Cycle not found.");
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
                ?? throw new ArgumentException("A condition uses a question that isn't on this cycle's application.");

            if (ChoiceTypes.Contains(question.Type))
            {
                if (!await db.Query("QuestionOptions").Where("Id", condition.OptionId ?? "").Where("QuestionId", question.Id).ExistsAsync())
                    throw new ArgumentException($"Choose an answer for \"{question.Text}\".");
                condition.TextContains = null;
            }
            else if (TextTypes.Contains(question.Type))
            {
                condition.TextContains = InputValidation.Required(condition.TextContains, "Text to look for", 255);
                condition.OptionId = null;
            }
            else
            {
                throw new ArgumentException("Conditions can only use choice or text questions.");
            }
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
    public string? TextContains { get; set; }
}
