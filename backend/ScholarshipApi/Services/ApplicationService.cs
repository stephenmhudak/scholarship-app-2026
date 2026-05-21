using System.Text.Json;
using SqlKata.Execution;
using ScholarshipApi.DTOs.Application;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

public class ApplicationService(QueryFactory db) : IApplicationService
{
    public async Task<IEnumerable<ApplicationDto>> ListForApplicantAsync(string applicantId) =>
        await db.Query("Applications").Where("ApplicantId", applicantId).GetAsync<ApplicationDto>();

    public async Task<ApplicationDto> GetAsync(string id, string requestingUserId, string requestingRole)
    {
        var app = await db.Query("Applications as a")
            .Join("Users as u", "u.Id", "a.ApplicantId")
            .Select("a.Id", "a.ApplicantId", "a.CycleId", "a.Status", "a.SubmittedAt", "a.CreatedAt",
                    "u.FirstName as ApplicantFirstName", "u.LastName as ApplicantLastName", "u.Email as ApplicantEmail")
            .Where("a.Id", id)
            .FirstOrDefaultAsync<ApplicationDto>()
            ?? throw new KeyNotFoundException("Application not found.");

        if (requestingRole == "applicant" && app.ApplicantId != requestingUserId)
            throw new UnauthorizedAccessException("Access denied.");

        var answers = await db.Query("ApplicationAnswers").Where("ApplicationId", id).GetAsync<ApplicationAnswer>();
        app.Answers = answers.Select(a => new AnswerDto
        {
            QuestionId = a.QuestionId,
            TextValue = a.TextValue,
            SelectedOptions = a.SelectedOptions is not null
                ? JsonSerializer.Deserialize<List<string>>(a.SelectedOptions)
                : null
        }).ToList();

        return app;
    }

    public async Task<string> CreateAsync(string applicantId)
    {
        var cycle = await db.Query("ScholarshipCycles").Where("IsActive", true).FirstOrDefaultAsync<ScholarshipCycle>()
            ?? throw new ArgumentException("No active scholarship cycle.");

        var id = Guid.NewGuid().ToString();
        await db.Query("Applications").InsertAsync(new
        {
            Id = id,
            ApplicantId = applicantId,
            CycleId = cycle.Id,
            Status = "draft",
            CreatedAt = DateTime.UtcNow
        });
        return id;
    }

    public async Task SaveDraftAsync(string id, string applicantId, SubmitApplicationRequest request)
    {
        var app = await db.Query("Applications").Where("Id", id).FirstOrDefaultAsync<Application>()
            ?? throw new KeyNotFoundException("Application not found.");

        if (app.ApplicantId != applicantId) throw new UnauthorizedAccessException("Access denied.");
        if (app.Status == "submitted") throw new ArgumentException("Cannot edit a submitted application.");

        await db.Query("ApplicationAnswers").Where("ApplicationId", id).DeleteAsync();

        foreach (var answer in request.Answers)
        {
            await db.Query("ApplicationAnswers").InsertAsync(new
            {
                Id = Guid.NewGuid().ToString(),
                ApplicationId = id,
                QuestionId = answer.QuestionId,
                TextValue = answer.TextValue,
                SelectedOptions = answer.SelectedOptions is not null
                    ? JsonSerializer.Serialize(answer.SelectedOptions)
                    : null
            });
        }
    }

    public async Task SubmitAsync(string id, string applicantId)
    {
        var app = await db.Query("Applications").Where("Id", id).FirstOrDefaultAsync<Application>()
            ?? throw new KeyNotFoundException("Application not found.");

        if (app.ApplicantId != applicantId) throw new UnauthorizedAccessException("Access denied.");
        if (app.Status != "draft") throw new ArgumentException("Only draft applications can be submitted.");

        await db.Query("Applications").Where("Id", id).UpdateAsync(new
        {
            Status = "submitted",
            SubmittedAt = DateTime.UtcNow
        });
    }

    public async Task<IEnumerable<ApplicationListDto>> AdminListAsync(string? status, string? search, int page, int pageSize)
    {
        var query = db.Query("Applications as a")
            .Join("Users as u", "u.Id", "a.ApplicantId")
            .Select("a.Id", "a.Status", "u.FirstName", "u.LastName", "a.SubmittedAt", "a.CreatedAt")
            .When(!string.IsNullOrEmpty(status), q => q.Where("a.Status", status))
            .When(!string.IsNullOrEmpty(search), q =>
                q.Where(inner => inner
                    .WhereLike("u.FirstName", $"%{search}%")
                    .OrWhereLike("u.LastName", $"%{search}%")
                    .OrWhereLike("u.Email", $"%{search}%")))
            .ForPage(page, pageSize);

        return await query.GetAsync<ApplicationListDto>();
    }

    public async Task<int> AdminCountAsync(string? status, string? search)
    {
        var query = db.Query("Applications as a")
            .Join("Users as u", "u.Id", "a.ApplicantId")
            .When(!string.IsNullOrEmpty(status), q => q.Where("a.Status", status))
            .When(!string.IsNullOrEmpty(search), q =>
                q.Where(inner => inner
                    .WhereLike("u.FirstName", $"%{search}%")
                    .OrWhereLike("u.LastName", $"%{search}%")
                    .OrWhereLike("u.Email", $"%{search}%")));

        return await query.CountAsync<int>();
    }

    public async Task UpdateStatusAsync(string id, string status) =>
        await db.Query("Applications").Where("Id", id).UpdateAsync(new { Status = status });

    public async Task AssignScorerAsync(string id, string scorerId)
    {
        var scorer = await db.Query("Users").Where("Id", scorerId).Where("Role", "scorer").FirstOrDefaultAsync<Models.User>()
            ?? throw new ArgumentException("Scorer not found.");

        var alreadyAssigned = await db.Query("ApplicationScorers")
            .Where("ApplicationId", id)
            .Where("ScoredById", scorerId)
            .ExistsAsync();

        if (!alreadyAssigned)
        {
            await db.Query("ApplicationScorers").InsertAsync(new
            {
                Id = Guid.NewGuid().ToString(),
                ApplicationId = id,
                ScoredById = scorerId,
                AssignedAt = DateTime.UtcNow
            });
        }
    }
}
