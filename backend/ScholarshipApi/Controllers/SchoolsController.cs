using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using SqlKata.Execution;
using ScholarshipApi.Models;

namespace ScholarshipApi.Controllers;

[ApiController]
[Route("api/schools")]
[Authorize(Policy = "SchoolAdmin")]
public class SchoolsController(QueryFactory db) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!;

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var schools = await db.Query("Schools").OrderBy("Name").GetAsync<School>();
        return Ok(schools);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SchoolRequest request)
    {
        var id = Guid.NewGuid().ToString();
        await db.Query("Schools").InsertAsync(new
        {
            Id = id,
            Name = request.Name,
            Address = request.Address,
            CreatedAt = DateTime.UtcNow
        });
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var school = await db.Query("Schools").Where("Id", id).FirstOrDefaultAsync<School>()
            ?? throw new KeyNotFoundException("School not found.");
        return Ok(school);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] SchoolRequest request)
    {
        await db.Query("Schools").Where("Id", id).UpdateAsync(new
        {
            Name = request.Name,
            Address = request.Address
        });
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        await db.Query("Schools").Where("Id", id).DeleteAsync();
        return NoContent();
    }

    [HttpGet("{id}/applicants")]
    [Authorize(Policy = "Staff")]
    public async Task<IActionResult> GetApplicants(string id, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = db.Query("Users as u")
            .LeftJoin("Applications as a", j => j.On("a.ApplicantId", "u.Id"))
            .LeftJoin("ScholarshipCycles as c", j => j.On("c.Id", "a.CycleId").Where("c.IsActive", true))
            .Select("u.Id", "u.FirstName", "u.LastName", "u.Email", "a.Status", "a.SubmittedAt")
            .Where("u.SchoolId", id)
            .Where("u.Role", "applicant")
            .When(!string.IsNullOrEmpty(search), q =>
                q.Where(inner => inner
                    .WhereLike("u.FirstName", $"%{search}%")
                    .OrWhereLike("u.LastName", $"%{search}%")
                    .OrWhereLike("u.Email", $"%{search}%")))
            .ForPage(page, pageSize);

        var applicants = await query.GetAsync<dynamic>();
        return Ok(applicants);
    }

    [HttpPost("{id}/counselors/invite")]
    public async Task<IActionResult> InviteCounselor(string id, [FromBody] InviteCounselorRequest request)
    {
        var school = await db.Query("Schools").Where("Id", id).FirstOrDefaultAsync<School>()
            ?? throw new KeyNotFoundException("School not found.");

        return Ok(new { message = $"Invitation sent to {request.Email} for school {school.Name}." });
    }
}

public class SchoolRequest
{
    public string Name { get; set; } = null!;
    public string Address { get; set; } = null!;
}

public class InviteCounselorRequest
{
    public string Email { get; set; } = null!;
}
