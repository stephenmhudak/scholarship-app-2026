using System.Text.Json;
using SqlKata.Execution;
using ScholarshipApi.DTOs.Application;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

public class ApplicationService(QueryFactory db, ApplicationAccess access) : IApplicationService
{
    public async Task<IEnumerable<ApplicationDto>> ListForApplicantAsync(string applicantId)
    {
        var apps = (await db.Query("Applications").Where("ApplicantId", applicantId)
            .GetAsync<ApplicationDto>()).ToList();
        await FillCycleInfoAsync(apps);
        return apps;
    }

    public async Task<IEnumerable<OpenCycleDto>> ListOpenCyclesAsync()
    {
        var now = DateTime.UtcNow;
        var cycles = await db.Query("ScholarshipCycles").Where("IsActive", true)
            .OrderBy("CloseDate").GetAsync<ScholarshipCycle>();
        return cycles.Where(c => CycleWindow.IsOpen(c, now)).Select(c => new OpenCycleDto
        {
            Id = c.Id,
            Name = c.Name,
            OpenDate = c.OpenDate,
            CloseDate = c.CloseDate,
        });
    }

    public async Task<ApplicationDto> GetAsync(string id, string requestingUserId, string requestingRole)
    {
        var app = await db.Query("Applications as a")
            .Join("Users as u", "u.Id", "a.ApplicantId")
            .Select("a.Id", "a.ApplicantId", "a.CycleId", "a.Status", "a.SubmittedAt", "a.CreatedAt",
                    "u.FirstName as ApplicantFirstName", "u.LastName as ApplicantLastName", "u.Email as ApplicantEmail")
            .Where("a.Id", id)
            .FirstOrDefaultAsync<ApplicationDto>()
            ?? throw new KeyNotFoundException("Application not found.");

        if (!await access.CanViewAsync(app.Id, app.ApplicantId, app.Status, app.CycleId, requestingUserId, requestingRole))
            throw new UnauthorizedAccessException("Access denied.");

        await FillCycleInfoAsync([app]);

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

    public async Task<string> CreateAsync(string applicantId, string? cycleId)
    {
        ScholarshipCycle cycle;
        if (string.IsNullOrEmpty(cycleId))
        {
            // No cycle chosen: fine when there's only one to choose from.
            var open = (await ListOpenCyclesAsync()).ToList();
            if (open.Count == 0) throw new ArgumentException("No scholarship cycle is currently open.");
            if (open.Count > 1) throw new ArgumentException("Choose which scholarship to apply for.");
            cycleId = open[0].Id;
        }

        cycle = await db.Query("ScholarshipCycles").Where("Id", cycleId).FirstOrDefaultAsync<ScholarshipCycle>()
            ?? throw new KeyNotFoundException("Scholarship cycle not found.");
        if (!CycleWindow.IsOpen(cycle, DateTime.UtcNow))
            throw new ArgumentException("This scholarship cycle is not open for applications.");

        // One application per cycle — starting again just returns the existing one.
        var existingId = await db.Query("Applications")
            .Where("ApplicantId", applicantId).Where("CycleId", cycle.Id)
            .Select("Id").FirstOrDefaultAsync<string>();
        if (existingId != null) return existingId;

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
        if (app.Status != "draft") throw new ArgumentException("Only draft applications can be edited.");
        await EnsureCycleOpenAsync(app.CycleId);

        var questions = (await db.Query("Questions").Where("CycleId", app.CycleId).WhereNull("ReferenceFormId")
            .GetAsync<Question>()).ToList();
        var questionIds = questions.Select(q => q.Id).ToHashSet();
        var richTextQuestionIds = questions.Where(QuestionRules.IsRichText).Select(q => q.Id).ToHashSet();
        var fileQuestionIds = questions.Where(q => q.Type == "file_upload").Select(q => q.Id).ToHashSet();

        // Keep answers to this cycle's questions that the applicant can see. A conditional question hidden by their
        // other answers loses its answer, so it can't count toward anything (references, scoring) later.
        var answers = request.Answers.Where(a => questionIds.Contains(a.QuestionId))
            .GroupBy(a => a.QuestionId).Select(g => g.First()).ToList();
        var conditions = await QuestionVisibility.LoadConditionsAsync(db, questionIds);
        var visible = QuestionVisibility.VisibleIds(questions, conditions,
            answers.ToDictionary(a => a.QuestionId, AnswerValue.From));
        answers = answers.Where(a => visible.Contains(a.QuestionId)).ToList();

        // A file answer grants access to the file (see ApplicationAccess), so it may only name the
        // applicant's own uploads: from this application, or one they copied answers from.
        var fileIds = answers
            .Where(a => fileQuestionIds.Contains(a.QuestionId) && !string.IsNullOrEmpty(a.TextValue))
            .Select(a => a.TextValue!).Distinct().ToList();
        if (fileIds.Count > 0)
        {
            var ownFileCount = await db.Query("ApplicationFiles as f")
                .Join("Applications as a", "a.Id", "f.ApplicationId")
                .WhereIn("f.Id", fileIds)
                .Where("a.ApplicantId", applicantId)
                .CountAsync<int>();
            if (ownFileCount != fileIds.Count)
                throw new ArgumentException("One of the uploaded files couldn't be found. Please upload it again.");
        }

        await db.Query("ApplicationAnswers").Where("ApplicationId", id).DeleteAsync();

        foreach (var answer in answers)
        {
            await db.Query("ApplicationAnswers").InsertAsync(new
            {
                Id = Guid.NewGuid().ToString(),
                ApplicationId = id,
                QuestionId = answer.QuestionId,
                TextValue = richTextQuestionIds.Contains(answer.QuestionId)
                    ? RichTextSanitizer.Sanitize(answer.TextValue)
                    : answer.TextValue,
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
        await EnsureCycleOpenAsync(app.CycleId);

        await EnsureVisibleQuestionsAnsweredAsync(app);

        var missing = await ReferenceRequirements.MissingAsync(db, id, app.CycleId);
        if (missing.Count > 0)
            throw new ArgumentException($"Your references aren't all in yet: {string.Join(", ", missing)}.");

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

    public async Task UpdateStatusAsync(string id, string status)
    {
        var updated = await db.Query("Applications").Where("Id", id).UpdateAsync(new { Status = InputValidation.ApplicationStatus(status) });
        if (updated == 0) throw new KeyNotFoundException("Application not found.");
    }

    public async Task<CopyAnswersResult> CopyAnswersAsync(string targetId, string sourceId, string applicantId)
    {
        var target = await db.Query("Applications").Where("Id", targetId).FirstOrDefaultAsync<Application>()
            ?? throw new KeyNotFoundException("Application not found.");
        var source = await db.Query("Applications").Where("Id", sourceId).FirstOrDefaultAsync<Application>()
            ?? throw new KeyNotFoundException("Application to copy from was not found.");

        if (target.ApplicantId != applicantId || source.ApplicantId != applicantId)
            throw new UnauthorizedAccessException("Access denied.");
        if (target.Status != "draft") throw new ArgumentException("Only draft applications can be edited.");
        if (source.CycleId == target.CycleId)
            throw new ArgumentException("Choose an application from a different scholarship cycle.");
        await EnsureCycleOpenAsync(target.CycleId);

        var sourceQuestions = await LoadQuestionsAsync(source.CycleId);
        var targetQuestions = await LoadQuestionsAsync(target.CycleId);

        var sourceAnswers = (await db.Query("ApplicationAnswers").Where("ApplicationId", sourceId)
            .GetAsync<ApplicationAnswer>()).Where(HasValue).ToDictionary(a => a.QuestionId);
        var answeredTargetIds = (await db.Query("ApplicationAnswers").Where("ApplicationId", targetId)
            .GetAsync<ApplicationAnswer>()).Where(HasValue).Select(a => a.QuestionId).ToHashSet();

        // Questions match when their details do; identical repeats (e.g. two "Name" questions) pair up in order.
        var sourceByKey = sourceQuestions.ToLookup(q => q.MatchKey);
        var result = new CopyAnswersResult();

        foreach (var group in targetQuestions.GroupBy(q => q.MatchKey))
        {
            foreach (var (to, from) in group.Zip(sourceByKey[group.Key]))
            {
                if (!sourceAnswers.TryGetValue(from.Question.Id, out var answer)) continue;
                if (answeredTargetIds.Contains(to.Question.Id))
                {
                    result.AlreadyAnswered++;
                    continue;
                }

                // Choice answers hold option ids; map them across by option text.
                var textValue = answer.TextValue;
                var selectedOptions = answer.SelectedOptions;
                if (to.Question.Type == "single_choice" && textValue != null)
                    textValue = MapOption(from, to, textValue);
                if (selectedOptions != null)
                {
                    var ids = JsonSerializer.Deserialize<List<string>>(selectedOptions) ?? [];
                    var mapped = ids.Select(id => MapOption(from, to, id)).OfType<string>().ToList();
                    selectedOptions = JsonSerializer.Serialize(mapped);
                }

                await db.Query("ApplicationAnswers")
                    .Where("ApplicationId", targetId).Where("QuestionId", to.Question.Id).DeleteAsync();
                await db.Query("ApplicationAnswers").InsertAsync(new
                {
                    Id = Guid.NewGuid().ToString(),
                    ApplicationId = targetId,
                    QuestionId = to.Question.Id,
                    TextValue = textValue,
                    SelectedOptions = selectedOptions,
                });
                result.Copied++;
            }
        }

        return result;
    }

    private record MatchableQuestion(Question Question, List<QuestionOption> Options, string MatchKey);

    private async Task<List<MatchableQuestion>> LoadQuestionsAsync(string cycleId)
    {
        var questions = (await db.Query("Questions").Where("CycleId", cycleId).WhereNull("ReferenceFormId").OrderBy("Order")
            .GetAsync<Question>()).ToList();
        var options = questions.Count == 0
            ? Enumerable.Empty<QuestionOption>().ToLookup(o => o.QuestionId)
            : (await db.Query("QuestionOptions").WhereIn("QuestionId", questions.Select(q => q.Id))
                .GetAsync<QuestionOption>()).ToLookup(o => o.QuestionId);

        return questions.Select(q =>
        {
            var opts = options[q.Id].ToList();
            // Same wording, type, rich text setting and set of options. Help text, required and
            // length/format rules may differ — the applicant can still edit the copied answer.
            var key = string.Join("\u001f",
                Normalize(q.Text), q.Type, QuestionRules.IsRichText(q),
                string.Join("\u001e", opts.Select(o => Normalize(o.Text)).Order(StringComparer.Ordinal)));
            return new MatchableQuestion(q, opts, key);
        }).ToList();
    }

    private static string? MapOption(MatchableQuestion from, MatchableQuestion to, string optionId)
    {
        var text = from.Options.FirstOrDefault(o => o.Id == optionId)?.Text;
        return text == null ? null : to.Options.FirstOrDefault(o => Normalize(o.Text) == Normalize(text))?.Id;
    }

    private static string Normalize(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    // Only what the applicant could see counts: answers left on questions their other answers now hide are removed
    // (e.g. after copying answers), and every required question that's shown needs an answer.
    private async Task EnsureVisibleQuestionsAnsweredAsync(Application app)
    {
        var questions = (await db.Query("Questions").Where("CycleId", app.CycleId).WhereNull("ReferenceFormId")
            .OrderBy("Order").GetAsync<Question>()).ToList();
        var conditions = await QuestionVisibility.LoadConditionsAsync(db, questions.Select(q => q.Id));
        var saved = (await db.Query("ApplicationAnswers").Where("ApplicationId", app.Id).GetAsync<ApplicationAnswer>()).ToList();
        var visible = QuestionVisibility.VisibleIds(questions, conditions,
            saved.GroupBy(a => a.QuestionId).ToDictionary(g => g.Key, g => AnswerValue.From(g.First())));

        var hidden = saved.Where(a => !visible.Contains(a.QuestionId)).Select(a => a.Id).ToList();
        if (hidden.Count > 0) await db.Query("ApplicationAnswers").WhereIn("Id", hidden).DeleteAsync();

        var answered = saved.Where(a => visible.Contains(a.QuestionId) && HasValue(a)).Select(a => a.QuestionId).ToHashSet();
        var unanswered = questions.Where(q => q.IsRequired && visible.Contains(q.Id) && !answered.Contains(q.Id))
            .Select(q => q.Text.Length <= 60 ? q.Text : q.Text[..57] + "…").ToList();
        if (unanswered.Count > 0)
            throw new ArgumentException("Please answer every required question before submitting: "
                + string.Join("; ", unanswered.Take(5)) + (unanswered.Count > 5 ? $"; and {unanswered.Count - 5} more." : "."));
    }

    private static bool HasValue(ApplicationAnswer a) =>
        !string.IsNullOrWhiteSpace(a.TextValue) || (a.SelectedOptions != null && a.SelectedOptions != "[]");

    private async Task EnsureCycleOpenAsync(string cycleId)
    {
        var cycle = await db.Query("ScholarshipCycles").Where("Id", cycleId).FirstOrDefaultAsync<ScholarshipCycle>();
        if (cycle == null || !CycleWindow.IsOpen(cycle, DateTime.UtcNow))
            throw new ArgumentException("This scholarship cycle has closed.");
    }

    private async Task FillCycleInfoAsync(IReadOnlyCollection<ApplicationDto> apps)
    {
        if (apps.Count == 0) return;
        var cycles = (await db.Query("ScholarshipCycles").WhereIn("Id", apps.Select(a => a.CycleId).Distinct())
            .GetAsync<ScholarshipCycle>()).ToDictionary(c => c.Id);
        var now = DateTime.UtcNow;
        foreach (var app in apps)
        {
            if (!cycles.TryGetValue(app.CycleId, out var cycle)) continue;
            app.CycleName = cycle.Name;
            app.CycleCloseDate = cycle.CloseDate;
            app.IsCycleOpen = CycleWindow.IsOpen(cycle, now);
        }
    }

}
