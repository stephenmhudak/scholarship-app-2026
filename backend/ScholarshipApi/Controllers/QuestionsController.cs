using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlKata.Execution;
using ScholarshipApi.Models;

namespace ScholarshipApi.Controllers;

[ApiController]
[Route("api/cycles/{cycleId}/questions")]
[Authorize]
public class QuestionsController(QueryFactory db) : ControllerBase
{
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
            q.Id, q.CycleId, q.Text, q.Type, q.Order, q.IsRequired,
            Options = options.Where(o => o.QuestionId == q.Id)
        });

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "AppAdmin")]
    public async Task<IActionResult> Create(string cycleId, [FromBody] QuestionRequest request)
    {
        var id = Guid.NewGuid().ToString();
        await db.Query("Questions").InsertAsync(new
        {
            Id = id,
            CycleId = cycleId,
            Text = request.Text,
            Type = request.Type,
            Order = request.Order,
            IsRequired = request.IsRequired
        });
        return CreatedAtAction(nameof(List), new { cycleId }, new { id });
    }

    [HttpPut("{questionId}")]
    [Authorize(Policy = "AppAdmin")]
    public async Task<IActionResult> Update(string cycleId, string questionId, [FromBody] QuestionRequest request)
    {
        await db.Query("Questions").Where("Id", questionId).Where("CycleId", cycleId).UpdateAsync(new
        {
            Text = request.Text,
            Type = request.Type,
            Order = request.Order,
            IsRequired = request.IsRequired
        });
        return NoContent();
    }

    [HttpDelete("{questionId}")]
    [Authorize(Policy = "AppAdmin")]
    public async Task<IActionResult> Delete(string cycleId, string questionId)
    {
        await db.Query("Questions").Where("Id", questionId).Where("CycleId", cycleId).DeleteAsync();
        return NoContent();
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

public class QuestionRequest
{
    public string Text { get; set; } = null!;
    public string Type { get; set; } = null!;
    public int Order { get; set; }
    public bool IsRequired { get; set; }
}

public class ReorderItem
{
    public string Id { get; set; } = null!;
    public int Order { get; set; }
}
