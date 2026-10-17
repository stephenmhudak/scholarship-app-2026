using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlKata.Execution;
using ScholarshipApi.Authorization;
using ScholarshipApi.Models;
using ScholarshipApi.Services;

namespace ScholarshipApi.Controllers;

/// <summary>
/// Award letters. App admins write a cycle's base letters (one is the default). Each scholarship uses its own letter
/// if it has one, otherwise the default base letter; its own letter can be written by app admins or the accounts
/// assigned to award it, who also generate the PDFs (<see cref="LetterPdf"/>, fields from <see cref="LetterMerge"/>).
/// </summary>
[ApiController]
[Authorize]
public class LettersController(QueryFactory db) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!;
    private bool IsAdmin => AppRoles.IsAdmin(User.FindFirstValue(ClaimTypes.Role));

    // ── Base letters (app admins) ────────────────────────────────────────────

    [HttpGet("api/cycles/{cycleId}/letters")]
    [Authorize(Policy = "AppAdmin")]
    public async Task<IActionResult> List(string cycleId)
    {
        var letters = (await db.Query("LetterTemplates").Where("CycleId", cycleId).OrderBy("Name").GetAsync<LetterTemplate>()).ToList();
        var scholarships = await db.Query("Scholarships").Where("CycleId", cycleId).OrderBy("Order", "CreatedAt").GetAsync<Scholarship>();
        // How many awards each has, so admins can download their letters from here
        var awardCounts = (await db.Query("ScholarshipAwards as w").Join("Scholarships as s", "s.Id", "w.ScholarshipId")
                .Where("s.CycleId", cycleId).GroupBy("w.ScholarshipId")
                .SelectRaw("w.ScholarshipId AS ScholarshipId, COUNT(*) AS Total").GetAsync<AwardCountRow>())
            .ToDictionary(r => r.ScholarshipId, r => r.Total);
        return Ok(new
        {
            BaseLetters = letters.Where(l => l.ScholarshipId is null).OrderByDescending(l => l.IsDefault).Select(ToDto),
            Scholarships = scholarships.Select(s => new
            {
                s.Id,
                s.Name,
                AwardCount = awardCounts.GetValueOrDefault(s.Id),
                Letter = letters.Where(l => l.ScholarshipId == s.Id).Select(ToDto).FirstOrDefault(),
            }),
        });
    }

    [HttpPost("api/cycles/{cycleId}/letters")]
    [Authorize(Policy = "AppAdmin")]
    public async Task<IActionResult> Create(string cycleId, [FromBody] LetterRequest request)
    {
        if (!await db.Query("ScholarshipCycles").Where("Id", cycleId).ExistsAsync()) throw new KeyNotFoundException("Cycle not found.");
        var (name, body) = Validate(request);
        var id = Guid.NewGuid().ToString();
        // The cycle's first base letter is its default
        var isDefault = request.IsDefault || !await db.Query("LetterTemplates").Where("CycleId", cycleId).WhereNull("ScholarshipId").ExistsAsync();
        if (isDefault) await ClearDefaultAsync(cycleId);
        await db.Query("LetterTemplates").InsertAsync(new
        {
            Id = id, CycleId = cycleId, Name = name, Body = body, IsDefault = isDefault,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, UpdatedById = UserId,
        });
        return Ok(new { id });
    }

    [HttpPut("api/cycles/{cycleId}/letters/{letterId}")]
    [Authorize(Policy = "AppAdmin")]
    public async Task<IActionResult> Update(string cycleId, string letterId, [FromBody] LetterRequest request)
    {
        var letter = await GetBaseLetterAsync(cycleId, letterId);
        var (name, body) = Validate(request);
        if (request.IsDefault && !letter.IsDefault) await ClearDefaultAsync(cycleId);
        await db.Query("LetterTemplates").Where("Id", letterId).UpdateAsync(new
        {
            Name = name, Body = body, IsDefault = request.IsDefault || letter.IsDefault, UpdatedAt = DateTime.UtcNow, UpdatedById = UserId,
        });
        return NoContent();
    }

    [HttpDelete("api/cycles/{cycleId}/letters/{letterId}")]
    [Authorize(Policy = "AppAdmin")]
    public async Task<IActionResult> Delete(string cycleId, string letterId)
    {
        var letter = await GetBaseLetterAsync(cycleId, letterId);
        await db.Query("LetterTemplates").Where("Id", letterId).DeleteAsync();
        // Another base letter takes over as the default
        if (letter.IsDefault)
        {
            var next = await db.Query("LetterTemplates").Where("CycleId", cycleId).WhereNull("ScholarshipId")
                .OrderBy("Name").Select("Id").FirstOrDefaultAsync<string>();
            if (next is not null) await db.Query("LetterTemplates").Where("Id", next).UpdateAsync(new { IsDefault = true });
        }
        return NoContent();
    }

    [HttpPost("api/cycles/{cycleId}/letters/preview")]
    [Authorize(Policy = "AppAdmin")]
    public async Task<IActionResult> PreviewBase(string cycleId, [FromBody] LetterRequest request)
    {
        if (!await db.Query("ScholarshipCycles").Where("Id", cycleId).ExistsAsync()) throw new KeyNotFoundException("Cycle not found.");
        var body = RichTextSanitizer.Sanitize(request.Body) ?? "";
        var values = await LetterMerge.SampleAsync(db, cycleId, null);
        return Pdf(LetterPdf.Render([LetterMerge.Apply(body, values)]), "letter-preview.pdf", inline: true);
    }

    // Merge fields for the editor: built-in ones and the cycle's application questions (for {{answer:…}})
    [HttpGet("api/cycles/{cycleId}/letter-fields")]
    public async Task<IActionResult> Fields(string cycleId)
    {
        if (!IsAdmin && !await db.Query("ScholarshipAwarders as a").Join("Scholarships as s", "s.Id", "a.ScholarshipId")
                .Where("s.CycleId", cycleId).Where("a.UserId", UserId).ExistsAsync())
            throw new UnauthorizedAccessException("Access denied.");
        var questions = await db.Query("Questions").Where("CycleId", cycleId).WhereNull("ReferenceFormId")
            .WhereNotIn("Type", new[] { "file_upload" }).OrderBy("Order").Select("Text").GetAsync<string>();
        return Ok(new
        {
            Fields = LetterMerge.Fields.Select(f => new { Token = $"{{{{{f.Token}}}}}", f.Label }),
            Answers = questions.Distinct().Select(t => new { Token = $"{{{{answer:{t}}}}}", Label = t }),
        });
    }

    // ── A scholarship's letter (app admins and its awarders) ─────────────────

    [HttpGet("api/scholarships/{scholarshipId}/letter")]
    public async Task<IActionResult> GetScholarshipLetter(string scholarshipId)
    {
        var scholarship = await GetManageableAsync(scholarshipId);
        var letters = (await db.Query("LetterTemplates").Where("CycleId", scholarship.CycleId).GetAsync<LetterTemplate>()).ToList();
        var own = letters.FirstOrDefault(l => l.ScholarshipId == scholarshipId);
        var bases = letters.Where(l => l.ScholarshipId is null).OrderByDescending(l => l.IsDefault).ThenBy(l => l.Name).ToList();
        return Ok(new
        {
            ScholarshipName = scholarship.Name,
            scholarship.CycleId,
            Own = own is null ? null : ToDto(own),
            // What it uses when it has no letter of its own, and what its own letter can start from
            Default = bases.Where(l => l.IsDefault).Select(ToDto).FirstOrDefault(),
            BaseLetters = bases.Select(ToDto),
        });
    }

    [HttpPut("api/scholarships/{scholarshipId}/letter")]
    public async Task<IActionResult> SaveScholarshipLetter(string scholarshipId, [FromBody] LetterRequest request)
    {
        var scholarship = await GetManageableAsync(scholarshipId);
        var (name, body) = Validate(request with { Name = string.IsNullOrWhiteSpace(request.Name) ? scholarship.Name : request.Name });
        var updated = await db.Query("LetterTemplates").Where("ScholarshipId", scholarshipId)
            .UpdateAsync(new { Name = name, Body = body, UpdatedAt = DateTime.UtcNow, UpdatedById = UserId });
        if (updated == 0)
            await db.Query("LetterTemplates").InsertAsync(new
            {
                Id = Guid.NewGuid().ToString(), scholarship.CycleId, ScholarshipId = scholarshipId, Name = name, Body = body,
                IsDefault = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, UpdatedById = UserId,
            });
        return NoContent();
    }

    // Back to the cycle's default letter
    [HttpDelete("api/scholarships/{scholarshipId}/letter")]
    public async Task<IActionResult> DeleteScholarshipLetter(string scholarshipId)
    {
        await GetManageableAsync(scholarshipId);
        await db.Query("LetterTemplates").Where("ScholarshipId", scholarshipId).DeleteAsync();
        return NoContent();
    }

    // Preview a draft of the scholarship's letter, for its first awardee (or sample values if none yet)
    [HttpPost("api/scholarships/{scholarshipId}/letter/preview")]
    public async Task<IActionResult> PreviewScholarshipLetter(string scholarshipId, [FromBody] LetterRequest request)
    {
        var scholarship = await GetManageableAsync(scholarshipId);
        var body = RichTextSanitizer.Sanitize(request.Body) ?? "";
        var award = await db.Query("ScholarshipAwards").Where("ScholarshipId", scholarshipId).OrderBy("AwardedAt")
            .FirstOrDefaultAsync<ScholarshipAward>();
        var values = award is null
            ? await LetterMerge.SampleAsync(db, scholarship.CycleId, scholarship)
            : await LetterMerge.ForAwardAsync(db, scholarship, award);
        return Pdf(LetterPdf.Render([LetterMerge.Apply(body, values)]), "letter-preview.pdf", inline: true);
    }

    // ── Generated letters ────────────────────────────────────────────────────

    [HttpGet("api/scholarships/{scholarshipId}/letters/{applicationId}")]
    public async Task<IActionResult> AwardLetter(string scholarshipId, string applicationId)
    {
        var scholarship = await GetManageableAsync(scholarshipId);
        var award = await db.Query("ScholarshipAwards").Where("ScholarshipId", scholarshipId).Where("ApplicationId", applicationId)
            .FirstOrDefaultAsync<ScholarshipAward>() ?? throw new KeyNotFoundException("That application hasn't been awarded this scholarship.");
        var body = await LetterBodyAsync(scholarship);
        var values = await LetterMerge.ForAwardAsync(db, scholarship, award);
        return Pdf(LetterPdf.Render([LetterMerge.Apply(body, values)]),
            $"{FileSafe(scholarship.Name)} - {FileSafe(values.Fields["awardee_name"])}.pdf");
    }

    // Every awardee's letter in one PDF, a page (or more) each
    [HttpGet("api/scholarships/{scholarshipId}/letters")]
    public async Task<IActionResult> AllAwardLetters(string scholarshipId)
    {
        var scholarship = await GetManageableAsync(scholarshipId);
        var awards = (await db.Query("ScholarshipAwards").Where("ScholarshipId", scholarshipId).OrderBy("AwardedAt")
            .GetAsync<ScholarshipAward>()).ToList();
        if (awards.Count == 0) throw new ArgumentException("This scholarship hasn't been awarded to anyone yet.");
        var body = await LetterBodyAsync(scholarship);
        var pages = new List<string>();
        foreach (var award in awards) pages.Add(LetterMerge.Apply(body, await LetterMerge.ForAwardAsync(db, scholarship, award)));
        return Pdf(LetterPdf.Render(pages), $"{FileSafe(scholarship.Name)} - award letters.pdf");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static object ToDto(LetterTemplate l) => new { l.Id, l.Name, l.Body, l.IsDefault, l.ScholarshipId, l.UpdatedAt };

    // Its own letter, else the cycle's default base letter
    private async Task<string> LetterBodyAsync(Scholarship scholarship)
    {
        var letters = (await db.Query("LetterTemplates").Where("CycleId", scholarship.CycleId).GetAsync<LetterTemplate>()).ToList();
        var letter = letters.FirstOrDefault(l => l.ScholarshipId == scholarship.Id) ?? letters.FirstOrDefault(l => l.ScholarshipId is null && l.IsDefault)
            ?? throw new ArgumentException("There's no award letter yet. Write one for this scholarship, or ask an admin to add a default letter for the cycle.");
        return letter.Body;
    }

    private async Task<Scholarship> GetManageableAsync(string scholarshipId)
    {
        var scholarship = await db.Query("Scholarships").Where("Id", scholarshipId).FirstOrDefaultAsync<Scholarship>()
            ?? throw new KeyNotFoundException("Scholarship not found.");
        if (!IsAdmin && !await db.Query("ScholarshipAwarders").Where("ScholarshipId", scholarshipId).Where("UserId", UserId).ExistsAsync())
            throw new UnauthorizedAccessException("You aren't assigned to this scholarship.");
        return scholarship;
    }

    private async Task<LetterTemplate> GetBaseLetterAsync(string cycleId, string letterId) =>
        await db.Query("LetterTemplates").Where("Id", letterId).Where("CycleId", cycleId).WhereNull("ScholarshipId")
            .FirstOrDefaultAsync<LetterTemplate>() ?? throw new KeyNotFoundException("Letter not found.");

    private Task<int> ClearDefaultAsync(string cycleId) =>
        db.Query("LetterTemplates").Where("CycleId", cycleId).WhereNull("ScholarshipId").UpdateAsync(new { IsDefault = false });

    private static (string Name, string Body) Validate(LetterRequest request) =>
        (InputValidation.Required(request.Name, "Letter name", 255),
         RichTextSanitizer.Sanitize(request.Body) ?? throw new ArgumentException("Write the letter before saving it."));

    private FileContentResult Pdf(byte[] bytes, string fileName, bool inline = false) =>
        inline ? File(bytes, "application/pdf") : File(bytes, "application/pdf", fileName);

    private class AwardCountRow
    {
        public string ScholarshipId { get; set; } = null!;
        public int Total { get; set; }
    }

    private static string FileSafe(string text) =>
        string.Concat(text.Split(Path.GetInvalidFileNameChars())).Trim() is { Length: > 0 } safe ? safe : "letter";
}

public record LetterRequest
{
    public string? Name { get; init; }
    /// <summary>HTML from the editor, with merge fields like {{awardee_name}} or {{answer:Question text}}.</summary>
    public string? Body { get; init; }
    /// <summary>Base letters only: make this the cycle's default.</summary>
    public bool IsDefault { get; init; }
}
