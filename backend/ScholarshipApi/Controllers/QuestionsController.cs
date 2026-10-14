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
public class QuestionsController(QueryFactory db, IAuthorizationService authorization) : ControllerBase
{
    // camelCase keys — the frontend reads validationRules.numberOnly etc. straight from the stored JSON
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Without referenceFormId: the application's questions, for anyone signed in. With it: that reference form's
    // questions, for admins building the form only; referees get them through their code (/api/reference-links).
    [HttpGet]
    public async Task<IActionResult> List(string cycleId, [FromQuery] string? referenceFormId)
    {
        if (referenceFormId is not null && !(await authorization.AuthorizeAsync(User, "AppAdmin")).Succeeded)
            return Forbid();

        var questions = await db.Query("Questions")
            .Where("CycleId", cycleId)
            .When(referenceFormId is null, q => q.WhereNull("ReferenceFormId"), q => q.Where("ReferenceFormId", referenceFormId))
            .OrderBy("Order")
            .GetAsync<Question>();

        var questionList = questions.ToList();
        var questionIds = questionList.Select(q => q.Id).ToList();

        var options = questionIds.Count > 0
            ? await db.Query("QuestionOptions").WhereIn("QuestionId", questionIds).OrderBy("Order").GetAsync<QuestionOption>()
            : [];
        var conditions = await QuestionVisibility.LoadConditionsAsync(db, questionIds);

        var result = questionList.Select(q => new
        {
            q.Id,
            q.CycleId,
            q.Text,
            q.Type,
            q.Order,
            q.IsRequired,
            q.SectionId,
            q.ReferenceFormId,
            q.Description,
            ValidationRules = q.ValidationRules != null
                ? JsonSerializer.Deserialize<JsonElement>(q.ValidationRules)
                : (JsonElement?)null,
            Options = options.Where(o => o.QuestionId == q.Id).Select(o => new { o.Id, o.Text, o.Order }),
            // The form shows a conditional question only while these match (frontend/src/utils/questionVisibility.js)
            q.Visibility,
            q.ConditionMatch,
            Conditions = conditions[q.Id].Select(c => new { c.SourceQuestionId, c.OptionId, c.TextContains }),
        });

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "AppAdmin")]
    public async Task<IActionResult> Create(string cycleId, [FromBody] QuestionRequest request, [FromQuery] string? referenceFormId)
    {
        if (!await db.Query("ScholarshipCycles").Where("Id", cycleId).ExistsAsync()) throw new KeyNotFoundException("Cycle not found.");
        if (referenceFormId is not null
            && !await db.Query("ReferenceForms").Where("Id", referenceFormId).Where("CycleId", cycleId).ExistsAsync())
            throw new KeyNotFoundException("Reference form not found.");
        // New questions go last (in their section, or on their reference form). Orders can have gaps after moves,
        // so it's worked out here rather than taken from the client.
        request.Order = (await db.Query("Questions").Where("CycleId", cycleId)
            .When(referenceFormId is null, q => q.WhereNull("ReferenceFormId"), q => q.Where("ReferenceFormId", referenceFormId))
            .MaxAsync<int?>("Order") ?? -1) + 1;
        await ValidateAsync(cycleId, request, referenceFormId, questionId: null);
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
            ReferenceFormId = referenceFormId,
            request.Description,
            ValidationRules = request.ValidationRules != null
                ? JsonSerializer.Serialize(request.ValidationRules, JsonOptions)
                : null,
            request.Visibility,
            request.ConditionMatch,
        });

        await SaveOptionsAsync(id, request.Options);
        await SaveConditionsAsync(id, request.Conditions);

        return CreatedAtAction(nameof(List), new { cycleId }, new { id });
    }

    [HttpPut("{questionId}")]
    [Authorize(Policy = "AppAdmin")]
    public async Task<IActionResult> Update(string cycleId, string questionId, [FromBody] QuestionRequest request)
    {
        var existing = await db.Query("Questions").Where("Id", questionId).Where("CycleId", cycleId).FirstOrDefaultAsync<Question>()
            ?? throw new KeyNotFoundException("Question not found.");
        await ValidateAsync(cycleId, request, existing.ReferenceFormId, questionId);
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
                : null,
            request.Visibility,
            request.ConditionMatch,
        });

        await SaveOptionsAsync(questionId, request.Options);
        await db.Query("QuestionConditions").Where("QuestionId", questionId).DeleteAsync();
        await SaveConditionsAsync(questionId, request.Conditions);

        // A new section, or options removed from this question, can strand other questions' conditions on it
        return Ok(await QuestionOrder.PruneConditionsAsync(db, cycleId));
    }

    [HttpDelete("{questionId}")]
    [Authorize(Policy = "AppAdmin")]
    public Task<IActionResult> Delete(string cycleId, string questionId, [FromQuery] bool force = false) =>
        DeleteQuestionsAsync(cycleId, [questionId], force);

    // Several questions at once (application or reference form questions), with the same dependency check
    [HttpPost("bulk-delete")]
    [Authorize(Policy = "AppAdmin")]
    public Task<IActionResult> BulkDelete(string cycleId, [FromBody] BulkQuestionsRequest request, [FromQuery] bool force = false) =>
        DeleteQuestionsAsync(cycleId, request.QuestionIds, force);

    // Moves application questions into a section (null = ungrouped), after the questions already there,
    // keeping their order relative to each other
    [HttpPut("move")]
    [Authorize(Policy = "AppAdmin")]
    public async Task<IActionResult> Move(string cycleId, [FromBody] MoveQuestionsRequest request)
    {
        var ids = (request.QuestionIds ?? []).Distinct().ToList();
        if (ids.Count == 0) throw new ArgumentException("Choose at least one question.");
        var questions = (await db.Query("Questions").WhereIn("Id", ids).Where("CycleId", cycleId).WhereNull("ReferenceFormId")
            .OrderBy("Order").GetAsync<Question>()).ToList();
        if (questions.Count != ids.Count) throw new ArgumentException("Only this cycle's application questions can be moved between sections.");

        var sectionId = string.IsNullOrEmpty(request.SectionId) ? null : request.SectionId;
        if (sectionId is not null && !await db.Query("Sections").Where("Id", sectionId).Where("CycleId", cycleId).ExistsAsync())
            throw new ArgumentException("That section isn't part of this cycle.");

        // Order is cycle-wide (sections group by it), so putting them after the last question lands them at the end of the section
        var nextOrder = (await db.Query("Questions").Where("CycleId", cycleId).WhereNull("ReferenceFormId").MaxAsync<int?>("Order") ?? -1) + 1;
        if (db.Connection.State != System.Data.ConnectionState.Open) db.Connection.Open();
        using var transaction = db.Connection.BeginTransaction();
        foreach (var question in questions)
            await db.Query("Questions").Where("Id", question.Id).UpdateAsync(new { SectionId = sectionId, Order = nextOrder++ }, transaction);
        var result = await QuestionOrder.PruneConditionsAsync(db, cycleId, transaction);
        transaction.Commit();

        return Ok(result);
    }

    private async Task<IActionResult> DeleteQuestionsAsync(string cycleId, List<string>? questionIds, bool force)
    {
        var ids = (questionIds ?? []).Distinct().ToList();
        if (ids.Count == 0) throw new ArgumentException("Choose at least one question.");
        if (await db.Query("Questions").WhereIn("Id", ids).Where("CycleId", cycleId).CountAsync<int>() != ids.Count)
            throw new KeyNotFoundException("Question not found.");

        // ApplicationAnswers.QuestionId has no ON DELETE rule, so answers must go first — only when the admin confirms.
        // Reference answers, and reference/question conditions that look at these questions, go with them (ON DELETE CASCADE).
        // A conditional question left with no conditions is then shown to everyone, so the admin is warned about
        // those too (unless it's being deleted as well).
        var answerCount = await db.Query("ApplicationAnswers").WhereIn("QuestionId", ids).CountAsync<int>()
            + await db.Query("ReferenceAnswers").WhereIn("QuestionId", ids).CountAsync<int>();
        var conditionCount = await db.Query("ReferenceFormConditions").WhereIn("QuestionId", ids).CountAsync<int>();
        var dependentCount = await db.Query("QuestionConditions").WhereIn("SourceQuestionId", ids).WhereNotIn("QuestionId", ids)
            .CountAsync<int>();
        if ((answerCount > 0 || conditionCount > 0 || dependentCount > 0) && !force)
            return Conflict(new { error = "Other things depend on these questions.", answerCount, conditionCount, dependentCount });

        if (db.Connection.State != System.Data.ConnectionState.Open) db.Connection.Open();
        using var transaction = db.Connection.BeginTransaction();
        await db.Query("ApplicationAnswers").WhereIn("QuestionId", ids).DeleteAsync(transaction);
        await db.Query("Questions").WhereIn("Id", ids).Where("CycleId", cycleId).DeleteAsync(transaction);
        var result = await QuestionOrder.PruneConditionsAsync(db, cycleId, transaction);
        transaction.Commit();

        return Ok(result);
    }

    private static readonly HashSet<string> QuestionTypes =
        ["short_answer", "long_answer", "multiple_choice", "single_choice", "file_upload", "school_select", "date"];
    private const int MaxTextLength = 5000;

    // Normalizes the request in place: trimmed text, blank options dropped, options only kept for choice questions
    private async Task ValidateAsync(string cycleId, QuestionRequest request, string? referenceFormId, string? questionId)
    {
        request.Text = InputValidation.Required(request.Text, "Question text", MaxTextLength);
        request.Description = InputValidation.Optional(request.Description, "Description", MaxTextLength);
        if (request.Type is null || !QuestionTypes.Contains(request.Type)) throw new ArgumentException("Choose a valid question type.");

        if (referenceFormId is not null)
        {
            // Referees don't sign in, and the school list needs an account
            if (request.Type == "school_select") throw new ArgumentException("School select isn't available on reference forms.");
            request.SectionId = null;
        }

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

        await ValidateVisibilityAsync(cycleId, request, referenceFormId, questionId);

        var rules = request.ValidationRules;
        if (rules is null) return;
        if (rules.MinLength < 0 || rules.MaxLength < 0) throw new ArgumentException("Length limits can't be negative.");
        if (rules.MinLength > rules.MaxLength) throw new ArgumentException("The minimum length can't be more than the maximum.");
        if (rules.RichText && request.Type != "long_answer") throw new ArgumentException("Rich text is only available on long answer questions.");
    }

    private static readonly HashSet<string> ConditionChoiceTypes = ["single_choice", "multiple_choice"];
    private static readonly HashSet<string> ConditionTextTypes = ["short_answer", "long_answer"];

    // When the question is shown. Only application questions can be conditional, and only on other application
    // questions in the cycle, without any question ending up depending on itself.
    private async Task ValidateVisibilityAsync(string cycleId, QuestionRequest request, string? referenceFormId, string? questionId)
    {
        request.Visibility ??= "always";
        request.ConditionMatch ??= "any";
        if (request.Visibility is not ("always" or "conditional")) throw new ArgumentException("Choose when this question is shown.");
        if (request.ConditionMatch is not ("any" or "all")) throw new ArgumentException("Choose whether any or all conditions must match.");

        if (request.Visibility == "always" || referenceFormId is not null)
        {
            request.Visibility = "always";
            request.Conditions = null;
            return;
        }

        var conditions = request.Conditions ?? [];
        if (conditions.Count == 0) throw new ArgumentException("Add at least one answer that shows this question, or always show it.");

        foreach (var condition in conditions)
        {
            if (condition.SourceQuestionId == questionId) throw new ArgumentException("A question can't depend on its own answer.");
            var source = await db.Query("Questions").Where("Id", condition.SourceQuestionId ?? "").Where("CycleId", cycleId)
                .WhereNull("ReferenceFormId").FirstOrDefaultAsync<Question>()
                ?? throw new ArgumentException("A condition uses a question that isn't on this cycle's application.");

            if (ConditionChoiceTypes.Contains(source.Type))
            {
                if (!await db.Query("QuestionOptions").Where("Id", condition.OptionId ?? "").Where("QuestionId", source.Id).ExistsAsync())
                    throw new ArgumentException($"Choose an answer for \"{source.Text}\".");
                condition.TextContains = null;
            }
            else if (ConditionTextTypes.Contains(source.Type))
            {
                condition.TextContains = InputValidation.Required(condition.TextContains, "Text to look for", 255);
                condition.OptionId = null;
            }
            else
            {
                throw new ArgumentException("Conditions can only use choice or text questions.");
            }
        }

        // Only questions that come before this one on the form, where it will be after this save
        var questions = (await db.Query("Questions").Where("CycleId", cycleId).WhereNull("ReferenceFormId").GetAsync<Question>()).ToList();
        if (questionId is null) questions.Add(new Question { Id = "", SectionId = request.SectionId, Order = request.Order });
        var sections = await db.Query("Sections").Where("CycleId", cycleId).GetAsync<Section>();
        var position = QuestionOrder.Positions(questions, sections,
            q => q.Id == (questionId ?? "") ? (request.SectionId, request.Order) : (q.SectionId, q.Order));
        var own = position[questionId ?? ""];
        if (conditions.Any(c => position[c.SourceQuestionId!] >= own))
            throw new ArgumentException("A question can only depend on questions that come before it on the form.");

        // Before-only dependencies can't loop, but a stale condition elsewhere shouldn't either
        if (questionId is not null && await WouldCreateCycleAsync(cycleId, questionId, conditions.Select(c => c.SourceQuestionId!)))
            throw new ArgumentException("That would make this question depend on itself through another conditional question.");
    }

    // True when one of the new source questions already depends (directly or through others) on this question
    private async Task<bool> WouldCreateCycleAsync(string cycleId, string questionId, IEnumerable<string> sourceIds)
    {
        var dependsOn = (await db.Query("QuestionConditions as c")
                .Join("Questions as q", "q.Id", "c.QuestionId")
                .Where("q.CycleId", cycleId)
                .Where("c.QuestionId", "<>", questionId)
                .Select("c.QuestionId", "c.SourceQuestionId")
                .GetAsync<QuestionCondition>())
            .ToLookup(c => c.QuestionId, c => c.SourceQuestionId);

        var seen = new HashSet<string>();
        var pending = new Stack<string>(sourceIds);
        while (pending.Count > 0)
        {
            var id = pending.Pop();
            if (id == questionId) return true;
            if (!seen.Add(id)) continue;
            foreach (var next in dependsOn[id]) pending.Push(next);
        }
        return false;
    }

    private async Task SaveConditionsAsync(string questionId, List<QuestionConditionRequest>? conditions)
    {
        foreach (var c in conditions ?? [])
        {
            await db.Query("QuestionConditions").InsertAsync(new
            {
                Id = Guid.NewGuid().ToString(),
                QuestionId = questionId,
                c.SourceQuestionId,
                c.OptionId,
                c.TextContains,
            });
        }
    }

    // Keeps the id of every option whose text is unchanged, so saved answers and reference conditions that point
    // at an option survive edits to the question. Options no longer listed are removed.
    private async Task SaveOptionsAsync(string questionId, List<string>? options)
    {
        var existing = (await db.Query("QuestionOptions").Where("QuestionId", questionId).GetAsync<QuestionOption>()).ToList();
        var kept = new HashSet<string>();
        var texts = options ?? [];
        for (int i = 0; i < texts.Count; i++)
        {
            var match = existing.FirstOrDefault(o => o.Text == texts[i] && !kept.Contains(o.Id));
            if (match is not null)
            {
                kept.Add(match.Id);
                if (match.Order != i) await db.Query("QuestionOptions").Where("Id", match.Id).UpdateAsync(new { Order = i });
                continue;
            }
            await db.Query("QuestionOptions").InsertAsync(new
            {
                Id = Guid.NewGuid().ToString(),
                QuestionId = questionId,
                Text = texts[i],
                Order = i
            });
        }

        var removed = existing.Where(o => !kept.Contains(o.Id)).Select(o => o.Id).ToList();
        if (removed.Count > 0) await db.Query("QuestionOptions").WhereIn("Id", removed).DeleteAsync();
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
        return Ok(await QuestionOrder.PruneConditionsAsync(db, cycleId));
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
    /// <summary><c>always</c> (default), or <c>conditional</c>: only shown when <see cref="Conditions"/> match. Application questions only.</summary>
    public string? Visibility { get; set; }
    /// <summary><c>any</c> (default) or <c>all</c> of the conditions must match.</summary>
    public string? ConditionMatch { get; set; }
    public List<QuestionConditionRequest>? Conditions { get; set; }
}

public class BulkQuestionsRequest
{
    public List<string>? QuestionIds { get; set; }
}

public class MoveQuestionsRequest
{
    public List<string>? QuestionIds { get; set; }
    /// <summary>The section to move into; null or empty for ungrouped.</summary>
    public string? SectionId { get; set; }
}

public class QuestionConditionRequest
{
    /// <summary>An earlier application question whose answer decides whether this one is shown.</summary>
    public string? SourceQuestionId { get; set; }
    /// <summary>For choice questions: shown when this option is chosen.</summary>
    public string? OptionId { get; set; }
    /// <summary>For text questions: shown when the answer contains this text (ignoring case).</summary>
    public string? TextContains { get; set; }
}

public class ReorderItem
{
    public string Id { get; set; } = null!;
    public int Order { get; set; }
}
