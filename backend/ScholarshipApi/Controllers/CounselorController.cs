using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlKata;
using SqlKata.Execution;
using System.Net;
using ScholarshipApi.Models;
using ScholarshipApi.Services;

namespace ScholarshipApi.Controllers;

[ApiController]
[Route("api/counselor")]
[Authorize(Policy = "Counselor")]
public class CounselorController(QueryFactory db, IEmailService email, IConfiguration config, ILogger<CounselorController> logger) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!;

    // A school's students: applicants with the school on their account, or who picked it in a School Select answer on
    // any of their applications (self-registered applicants have no school on their account). Same rule as the admin
    // Applications list's school filter.
    private static Query StudentsAt(QueryFactory db, string schoolId) =>
        db.Query("Users as u")
            .Where("u.Role", "applicant")
            .Where(q => q
                .Where("u.SchoolId", schoolId)
                .OrWhereExists(new Query("Applications as sa")
                    .Join("ApplicationAnswers as saa", "saa.ApplicationId", "sa.Id")
                    .Join("Questions as sq", "sq.Id", "saa.QuestionId")
                    .WhereColumns("sa.ApplicantId", "=", "u.Id")
                    .Where("sq.Type", "school_select")
                    .Where("saa.TextValue", schoolId)));

    // Every applicant at the counselor's school, with their most recent application. An empty list is normal
    // (a new school, or no school on the account), not an error.
    [HttpGet("applicants")]
    public async Task<IActionResult> GetApplicants([FromQuery] string? search)
    {
        var counselor = await GetCounselorAsync();
        if (counselor.SchoolId is null) return Ok(Array.Empty<CounselorApplicantDto>());

        var applicants = (await StudentsAt(db, counselor.SchoolId)
            .When(!string.IsNullOrWhiteSpace(search), q =>
                q.Where(inner => inner
                    .WhereLike("u.FirstName", $"%{search}%")
                    .OrWhereLike("u.LastName", $"%{search}%")
                    .OrWhereLike("u.Email", $"%{search}%")))
            .OrderBy("u.LastName", "u.FirstName")
            .Select("u.*")
            .GetAsync<User>()).ToList();
        if (applicants.Count == 0) return Ok(Array.Empty<CounselorApplicantDto>());

        var latest = (await db.Query("Applications").WhereIn("ApplicantId", applicants.Select(a => a.Id)).GetAsync<Application>())
            .GroupBy(a => a.ApplicantId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.CreatedAt).First());

        return Ok(applicants.Select(a =>
        {
            latest.TryGetValue(a.Id, out var app);
            return new CounselorApplicantDto
            {
                Id = a.Id,
                Name = $"{a.FirstName} {a.LastName}".Trim(),
                Email = a.Email,
                ApplicationStatus = app?.Status,
                LastActivity = app is null ? null : app.SubmittedAt ?? app.CreatedAt,
            };
        }));
    }

    // Emails one of the school's applicants a reminder to finish their application
    [HttpPost("nudge/{applicantId}")]
    public async Task<IActionResult> Nudge(string applicantId)
    {
        var counselor = await GetCounselorAsync();
        var applicant = counselor.SchoolId is null ? null : await StudentsAt(db, counselor.SchoolId)
            .Where("u.Id", applicantId)
            .Select("u.*")
            .FirstOrDefaultAsync<User>();
        if (applicant is null) throw new KeyNotFoundException("Applicant not found at your school.");

        var counselorName = $"{counselor.FirstName} {counselor.LastName}".Trim();
        var link = $"{(config["App:BaseUrl"] ?? "http://localhost:5173").TrimEnd('/')}/dashboard";
        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:480px;margin:0 auto;color:#2B3674">
              <h2 style="margin-bottom:8px">A reminder about your scholarship application</h2>
              <p>Hi {WebUtility.HtmlEncode(applicant.FirstName)},</p>
              <p>{WebUtility.HtmlEncode(counselorName)} is reminding you to finish and submit your scholarship application before the deadline.</p>
              <p style="margin:28px 0">
                <a href="{link}" style="background:#4361EE;color:#fff;padding:12px 24px;border-radius:12px;text-decoration:none;font-weight:bold">Go to your application</a>
              </p>
            </div>
            """;
        var text = $"Hi {applicant.FirstName},\n\n{counselorName} is reminding you to finish and submit your scholarship application before the deadline.\n\nGo to your application: {link}";

        try
        {
            await email.SendAsync(applicant.Email, "Reminder: finish your scholarship application", html, text);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send counselor reminder to applicant {ApplicantId}", applicant.Id);
            throw new ArgumentException("The reminder couldn't be sent. Please try again shortly.");
        }
        return Ok(new { message = $"Reminder sent to {applicant.Email}." });
    }

    private async Task<User> GetCounselorAsync() =>
        await db.Query("Users").Where("Id", UserId).FirstOrDefaultAsync<User>()
            ?? throw new KeyNotFoundException("Counselor not found.");
}

public class CounselorApplicantDto
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    /// <summary>The most recent application's status, or null if they haven't started one.</summary>
    public string? ApplicationStatus { get; set; }
    public DateTime? LastActivity { get; set; }
}
