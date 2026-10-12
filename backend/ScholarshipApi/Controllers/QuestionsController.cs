using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlKata.Execution;
using ScholarshipApi.Models;
using ScholarshipApi.Services;

namespace ScholarshipApi.Controllers;

[ApiController]
[Route("api/cycles/{cycleId}/questions")]
[Authorize]
public class QuestionsController(QueryFactory db) : ControllerBase
{
    // camelCase keys — the frontend reads validationRules.numberOnly etc. straight from the stored JSON
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpGet]
    public async Task<IActionResult> List(string cycleId)
    {
        var questions = await db.Query("Questions")
            .Where("CycleId", cycleId)
            .OrderBy("Order")
            .GetAsync<Question>();

        var questionList = questions.ToList();
        var questionIds = questionList.Select(q => q.Id).ToList();

        var options = questionIds.Count > 0
            ? await db.Query("QuestionOptions").WhereIn("QuestionId", questionIds).OrderBy("Order").GetAsync<QuestionOption>()
            : [];

        var result = questionList.Select(q => new
        {
            q.Id,
            q.CycleId,
            q.Text,
            q.Type,
            q.Order,
            q.IsRequired,
            q.SectionId,
            q.Description,
            ValidationRules = q.ValidationRules != null
                ? JsonSerializer.Deserialize<JsonElement>(q.ValidationRules)
                : (JsonElement?)null,
            Options = options.Where(o => o.QuestionId == q.Id).Select(o => new { o.Id, o.Text, o.Order })
        });

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "AppAdmin")]
    public async Task<IActionResult> Create(string cycleId, [FromBody] QuestionRequest request)
    {
        if (!await db.Query("ScholarshipCycles").Where("Id", cycleId).ExistsAsync()) throw new KeyNotFoundException("Cycle not found.");
        await ValidateAsync(cycleId, request);
        var id = Guid.NewGuid().ToString();
        await db.Query("Questions").InsertAsync(new
        {
            Id = id,
            CycleId = cycleId,
            Text = request.Text,
            Type = request.Type,
            Order = request.Order,
            request.IsRequired,
            request.SectionId,
            request.Description,
            ValidationRules = request.ValidationRules != null
                ? JsonSerializer.Serialize(request.ValidationRules, JsonOptions)
                : null
        });

        await SaveOptionsAsync(id, request.Options);

        return CreatedAtAction(nameof(List), new { cycleId }, new { id });
    }

    [HttpPut("{questionId}")]
    [Authorize(Policy = "AppAdmin")]
    public async Task<IActionResult> Update(string cycleId, string questionId, [FromBody] QuestionRequest request)
    {
        if (!await db.Query("Questions").Where("Id", questionId).Where("CycleId", cycleId).ExistsAsync())
            throw new KeyNotFoundException("Question not found.");
        await ValidateAsync(cycleId, request);
        await db.Query("Questions").Where("Id", questionId).Where("CycleId", cycleId).UpdateAsync(new
        {
            Text = request.Text,
            Type = request.Type,
            Order = request.Order,
            request.IsRequired,
            request.SectionId,
            request.Description,
            ValidationRules = request.ValidationRules != null
                ? JsonSerializer.Serialize(request.ValidationRules, JsonOptions)
                : null
        });

        await db.Query("QuestionOptions").Where("QuestionId", questionId).DeleteAsync();
        await SaveOptionsAsync(questionId, request.Options);

        return NoContent();
    }

    [HttpDelete("{questionId}")]
    [Authorize(Policy = "AppAdmin")]
    public async Task<IActionResult> Delete(string cycleId, string questionId, [FromQuery] bool force = false)
    {
        // ApplicationAnswers.QuestionId has no ON DELETE rule, so answers must go first — only when the admin confirms.
        var answerCount = await db.Query("ApplicationAnswers").Where("QuestionId", questionId).CountAsync<int>();
        if (answerCount > 0 && !force)
            return Conflict(new { error = $"{answerCount} application(s) have answered this question.", answerCount });

        if (db.Connection.State != System.Data.ConnectionState.Open) db.Connection.Open();
        using var transaction = db.Connection.BeginTransaction();
        await db.Query("ApplicationAnswers").Where("QuestionId", questionId).DeleteAsync(transaction);
        await db.Query("Questions").Where("Id", questionId).Where("CycleId", cycleId).DeleteAsync(transaction);
        transaction.Commit();

        return NoContent();
    }

    private static readonly HashSet<string> QuestionTypes =
        ["short_answer", "long_answer", "multiple_choice", "single_choice", "file_upload", "school_select", "date"];
    private const int MaxTextLength = 5000;

    // Normalizes the request in place: trimmed text, blank options dropped, options only kept for choice questions
    private async Task ValidateAsync(string cycleId, QuestionRequest request)
    {
        request.Text = InputValidation.Required(request.Text, "Question text", MaxTextLength);
        request.Description = InputValidation.Optional(request.Description, "Description", MaxTextLength);
        if (request.Type is null || !QuestionTypes.Contains(request.Type)) throw new ArgumentException("Choose a valid question type.");

        if (!string.IsNullOrEmpty(request.SectionId)
            && !await db.Query("Sections").Where("Id", request.SectionId).Where("CycleId", cycleId).ExistsAsync())
            throw new ArgumentException("That section isn't part of this cycle.");
        if (string.IsNullOrEmpty(request.SectionId)) request.SectionId = null;

        var isChoice = request.Type is "multiple_choice" or "single_choice";
        request.Options = isChoice
            ? (request.Options ?? []).Select(o => o?.Trim() ?? "").Where(o => o.Length > 0).ToList()
            : null;
        if (isChoice && request.Options!.Count == 0) throw new ArgumentException("Add at least one option.");
        if (request.Options?.Any(o => o.Length > 500) == true) throw new ArgumentException("Options must be at most 500 characters.");

        var rules = request.ValidationRules;
        if (rules is null) return;
        if (rules.MinLength < 0 || rules.MaxLength < 0) throw new ArgumentException("Length limits can't be negative.");
        if (rules.MinLength > rules.MaxLength) throw new ArgumentException("The minimum length can't be more than the maximum.");
        if (rules.RichText && request.Type != "long_answer") throw new ArgumentException("Rich text is only available on long answer questions.");
    }

    private async Task SaveOptionsAsync(string questionId, List<string>? options)
    {
        if (options is null || options.Count == 0) return;
        for (int i = 0; i < options.Count; i++)
        {
            await db.Query("QuestionOptions").InsertAsync(new
            {
                Id = Guid.NewGuid().ToString(),
                QuestionId = questionId,
                Text = options[i],
                Order = i
            });
        }
    }

    [HttpPut("reorder")]
    [Authorize(Policy = "AppAdmin")]
    public async Task<IActionResult> Reorder(string cycleId, [FromBody] List<ReorderItem> items)
    {
        foreach (var item in items)
        {
            await db.Query("Questions")
                .Where("Id", item.Id)
                .Where("CycleId", cycleId)
                .UpdateAsync(new { Order = item.Order });
        }
        return NoContent();
    }
}

public class ValidationRulesRequest
{
    public bool NumberOnly { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public bool PhoneFormat { get; set; }
    public bool EmailFormat { get; set; }
    // long_answer only: answer with the Tiptap editor; HTML is sanitized on save.
    public bool RichText { get; set; }
}

public class QuestionRequest
{
    public string Text { get; set; } = null!;
    public string Type { get; set; } = null!;
    public int Order { get; set; }
    public bool IsRequired { get; set; }
    public string? SectionId { get; set; }
    public string? Description { get; set; }
    public List<string>? Options { get; set; }
    public ValidationRulesRequest? ValidationRules { get; set; }
}

public class ReorderItem
{
    public string Id { get; set; } = null!;
    public int Order { get; set; }
}
