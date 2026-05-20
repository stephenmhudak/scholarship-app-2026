using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlKata.Execution;
using ScholarshipApi.Models;

namespace ScholarshipApi.Controllers;

[ApiController]
[Route("api/cycles/{cycleId}/sections")]
[Authorize]
public class SectionsController(QueryFactory db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(string cycleId)
    {
        var sections = await db.Query("Sections")
            .Where("CycleId", cycleId)
            .OrderBy("Order")
            .GetAsync<Section>();
        return Ok(sections);
    }

    [HttpPost]
    [Authorize(Policy = "AppAdmin")]
    public async Task<IActionResult> Create(string cycleId, [FromBody] SectionRequest request)
    {
        var id = Guid.NewGuid().ToString();
        var maxOrder = await db.Query("Sections").Where("CycleId", cycleId).MaxAsync<int>("Order");
        await db.Query("Sections").InsertAsync(new
        {
            Id = id,
            CycleId = cycleId,
            Title = request.Title,
            Description = request.Description,
            Order = maxOrder + 1
        });
        return CreatedAtAction(nameof(List), new { cycleId }, new { id });
    }

    [HttpPut("{sectionId}")]
    [Authorize(Policy = "AppAdmin")]
    public async Task<IActionResult> Update(string cycleId, string sectionId, [FromBody] SectionRequest request)
    {
        await db.Query("Sections")
            .Where("Id", sectionId)
            .Where("CycleId", cycleId)
            .UpdateAsync(new
            {
                Title = request.Title,
                Description = request.Description
            });
        return NoContent();
    }

    [HttpDelete("{sectionId}")]
    [Authorize(Policy = "AppAdmin")]
    public async Task<IActionResult> Delete(string cycleId, string sectionId)
    {
        // Unlink questions from this section before deleting
        await db.Query("Questions").Where("SectionId", sectionId).UpdateAsync(new { SectionId = (string?)null });
        await db.Query("Sections").Where("Id", sectionId).Where("CycleId", cycleId).DeleteAsync();
        return NoContent();
    }

    [HttpPut("reorder")]
    [Authorize(Policy = "AppAdmin")]
    public async Task<IActionResult> Reorder(string cycleId, [FromBody] List<SectionReorderItem> items)
    {
        foreach (var item in items)
        {
            await db.Query("Sections")
                .Where("Id", item.Id)
                .Where("CycleId", cycleId)
                .UpdateAsync(new { Order = item.Order });
        }
        return NoContent();
    }
}

public class SectionRequest
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
}

public class SectionReorderItem
{
    public string Id { get; set; } = null!;
    public int Order { get; set; }
}
