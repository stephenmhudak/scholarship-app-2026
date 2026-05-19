using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlKata.Execution;
using ScholarshipApi.Models;

namespace ScholarshipApi.Controllers;

[ApiController]
[Route("api/counselor")]
[Authorize(Policy = "Counselor")]
public class CounselorController(QueryFactory db) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!;

    [HttpGet("applicants")]
    public async Task<IActionResult> GetApplicants([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var counselor = await db.Query("Users").Where("Id", UserId).FirstOrDefaultAsync<User>()
            ?? throw new KeyNotFoundException("Counselor not found.");

        if (counselor.SchoolId is null) return Ok(new List<object>());

        var query = db.Query("Users as u")
            .LeftJoin("Applications as a", j => j.On("a.ApplicantId", "u.Id"))
            .LeftJoin("ScholarshipCycles as c", j => j.On("c.Id", "a.CycleId").Where("c.IsActive", true))
            .Select("u.Id", "u.FirstName", "u.LastName", "u.Email", "a.Status", "a.SubmittedAt")
            .Where("u.SchoolId", counselor.SchoolId)
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

    [HttpPost("nudge/{applicantId}")]
    public async Task<IActionResult> Nudge(string applicantId)
    {
        var counselor = await db.Query("Users").Where("Id", UserId).FirstOrDefaultAsync<User>()
            ?? throw new KeyNotFoundException("Counselor not found.");

        var applicant = await db.Query("Users")
            .Where("Id", applicantId)
            .Where("SchoolId", counselor.SchoolId)
            .Where("Role", "applicant")
            .FirstOrDefaultAsync<User>()
            ?? throw new KeyNotFoundException("Applicant not found at your school.");

        return Ok(new { message = $"Reminder email sent to {applicant.Email}." });
    }
}
