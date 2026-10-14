using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using SqlKata.Execution;
using ScholarshipApi.Authorization;
using ScholarshipApi.DTOs.Application;
using ScholarshipApi.DTOs.Reference;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

/// <summary>
/// References: the applicant makes a link per required reference and shares it; the referee signs in (a referee,
/// counselor or school admin account), answers the cycle's reference form and signs it; scorers read and score
/// what was submitted. Applicants only ever see a reference's status, and referees see what they submitted but
/// never its scores.
/// </summary>
public class ReferenceService(
    QueryFactory db,
    IFileStorageService fileStorage,
    TotpSecretProtector protector,
    IEmailService email,
    ApplicationAccess access,
    IConfiguration config,
    ILogger<ReferenceService> logger) : IReferenceService
{
    private const int MaxFilesPerReference = 20;
    private const int MaxSignatureBytes = 1024 * 1024;
    private const int MaxAnswerLength = 20000;
    private const string SignatureDataUrlPrefix = "data:image/jpeg;base64,";

    // ── Applicant ────────────────────────────────────────────────────────────

    public async Task<ApplicationReferencesDto> ListForApplicationAsync(string applicationId, string userId, string role)
    {
        var app = await GetApplicationAsync(applicationId);
        if (!(AppRoles.IsAdmin(role) || (role == "applicant" && app.ApplicantId == userId)))
            throw new UnauthorizedAccessException("Access denied.");

        var cycle = await GetCycleAsync(app.CycleId);
        var requirements = await ReferenceRequirements.EvaluateAsync(db, app.Id, app.CycleId);
        var requests = (await db.Query("ReferenceRequests").Where("ApplicationId", app.Id).OrderBy("CreatedAt")
            .GetAsync<ReferenceRequest>()).ToLookup(r => r.FormId);
        var conditions = requirements.Count == 0
            ? Enumerable.Empty<ReferenceFormCondition>().ToLookup(c => c.FormId)
            : (await db.Query("ReferenceFormConditions").WhereIn("FormId", requirements.Select(r => r.Form.Id))
                .GetAsync<ReferenceFormCondition>()).ToLookup(c => c.FormId);

        // Every form, with its conditions, so the application form can show the ones the applicant's answers call
        // for as they answer, before saving
        return new ApplicationReferencesDto
        {
            CanRequest = app.Status == "draft" && CycleWindow.IsOpen(cycle, DateTime.UtcNow),
            Forms = requirements
                .Select(r => new ReferenceFormStatusDto
                {
                    FormId = r.Form.Id,
                    Title = r.Form.Title,
                    Description = r.Form.Description,
                    RequiredCount = r.Form.RequiredCount,
                    Required = r.Required,
                    Requirement = r.Form.Requirement,
                    Conditions = conditions[r.Form.Id].Select(c => new ReferenceConditionDto
                    {
                        QuestionId = c.QuestionId,
                        OptionId = c.OptionId,
                        SchoolId = c.SchoolId,
                        TextContains = c.TextContains,
                    }).ToList(),
                    Received = requests[r.Form.Id].Count(x => x.Status == "submitted"),
                    Requests = requests[r.Form.Id].Select(ToDto).ToList(),
                })
                .ToList(),
        };
    }

    public async Task<ReferenceRequestDto> CreateRequestAsync(string applicationId, string applicantId, CreateReferenceRequestBody body)
    {
        var app = await GetEditableApplicationAsync(applicationId, applicantId);
        var requirement = (await ReferenceRequirements.EvaluateAsync(db, app.Id, app.CycleId))
            .FirstOrDefault(r => r.Form.Id == body.FormId)
            ?? throw new ArgumentException("That reference isn't part of this application.");
        if (!requirement.Required)
            throw new ArgumentException("Your answers don't call for this reference. If you just changed them, save your application first.");

        var existing = await db.Query("ReferenceRequests").Where("ApplicationId", app.Id).Where("FormId", body.FormId).CountAsync<int>();
        if (existing >= requirement.Form.RequiredCount)
            throw new ArgumentException($"You already have a link for each {requirement.Form.Title} needed. Delete one that hasn't been used to make another.");

        var label = InputValidation.Optional(body.Label, "Name", 255);
        var code = ReferenceCode.Generate();
        var id = Guid.NewGuid().ToString();
        await db.Query("ReferenceRequests").InsertAsync(new
        {
            Id = id,
            ApplicationId = app.Id,
            FormId = body.FormId,
            Label = label,
            CodeHash = ReferenceCode.Hash(code),
            CodeEncrypted = protector.Protect(code, ReferenceCode.AssociatedData(id)),
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
        });

        return ToDto(await db.Query("ReferenceRequests").Where("Id", id).FirstAsync<ReferenceRequest>());
    }

    public async Task DeleteRequestAsync(string applicationId, string applicantId, string requestId)
    {
        var app = await GetEditableApplicationAsync(applicationId, applicantId);
        // Submitted references stay: an applicant can't withdraw what a referee wrote
        var request = await db.Query("ReferenceRequests").Where("Id", requestId).Where("ApplicationId", app.Id)
            .Where("Status", "pending").FirstOrDefaultAsync<ReferenceRequest>()
            ?? throw new ArgumentException("Only links that haven't been used yet can be deleted.");

        var files = await db.Query("ReferenceFiles").Where("RequestId", request.Id).GetAsync<ReferenceFile>();
        var deleted = await db.Query("ReferenceRequests").Where("Id", request.Id).Where("Status", "pending").DeleteAsync();
        if (deleted == 0) throw new ArgumentException("Only links that haven't been used yet can be deleted.");
        foreach (var file in files) await TryDeleteStoredAsync(file.StoragePath);
    }

    // ── Referee (signed in, through the code) ────────────────────────────────

    public async Task<ReferenceLinkDto> GetLinkAsync(string code, string userId)
    {
        var (request, app, cycle) = await FindLinkAsync(code, userId);
        var form = await db.Query("ReferenceForms").Where("Id", request.FormId).FirstAsync<ReferenceForm>();
        var applicant = await db.Query("Users").Where("Id", app.ApplicantId).FirstAsync<User>();
        var status = LinkStatus(request, cycle);

        return new ReferenceLinkDto
        {
            Status = status,
            ApplicantName = $"{applicant.FirstName} {applicant.LastName}".Trim(),
            SchoolName = await GetApplicantSchoolAsync(app, applicant),
            CycleName = cycle.Name,
            FormTitle = form.Title,
            FormDescription = form.Description,
            SubmittedReferenceId = request.Status == "submitted" && request.SubmittedById == userId ? request.Id : null,
            Questions = status == "open" ? await LoadFormQuestionsAsync(form.Id) : [],
        };
    }

    public async Task<string> UploadFileAsync(string code, string userId, string? questionId, IFormFile file)
    {
        var (request, _, _) = await OpenLinkAsync(code, userId);
        var isFileQuestion = await db.Query("Questions").Where("Id", questionId ?? "").Where("ReferenceFormId", request.FormId)
            .Where("Type", "file_upload").ExistsAsync();
        if (!isFileQuestion) throw new ArgumentException("That question doesn't take a file.");
        if (await db.Query("ReferenceFiles").Where("RequestId", request.Id).CountAsync<int>() >= MaxFilesPerReference)
            throw new ArgumentException("Too many files have been uploaded for this reference.");

        var storagePath = await fileStorage.StoreAsync(file, "reference-files");
        var id = Guid.NewGuid().ToString();
        try
        {
            var fileName = Path.GetFileName(file.FileName);
            await db.Query("ReferenceFiles").InsertAsync(new
            {
                Id = id,
                RequestId = request.Id,
                QuestionId = questionId,
                FileName = fileName.Length > 255 ? fileName[^255..] : fileName,
                StoragePath = storagePath,
                UploadedById = userId,
                UploadedAt = DateTime.UtcNow,
            });
        }
        catch
        {
            await TryDeleteStoredAsync(storagePath);
            throw;
        }
        return id;
    }

    public async Task SubmitAsync(string code, string userId, ReferenceSubmission submission)
    {
        var (request, app, cycle) = await OpenLinkAsync(code, userId);
        var signerName = InputValidation.Required(submission.SignerName, "Your name", 200);
        var signature = DecodeSignature(submission.Signature);

        var questions = (await db.Query("Questions").Where("ReferenceFormId", request.FormId).OrderBy("Order")
            .GetAsync<Question>()).ToList();
        var options = questions.Count == 0
            ? Enumerable.Empty<QuestionOption>().ToLookup(o => o.QuestionId)
            : (await db.Query("QuestionOptions").WhereIn("QuestionId", questions.Select(q => q.Id))
                .GetAsync<QuestionOption>()).ToLookup(o => o.QuestionId);
        // Only files this account uploaded, in case someone else opened the same code
        var files = (await db.Query("ReferenceFiles").Where("RequestId", request.Id).Where("UploadedById", userId)
            .GetAsync<ReferenceFile>()).ToList();
        var answers = (submission.Answers ?? []).Where(a => a.QuestionId is not null)
            .GroupBy(a => a.QuestionId).ToDictionary(g => g.Key, g => g.First());

        var rows = new List<object>();
        foreach (var question in questions)
        {
            answers.TryGetValue(question.Id, out var answer);
            var (text, selected) = NormalizeAnswer(question, answer, options[question.Id].ToList(), files);
            var isEmpty = text is null && (selected is null || selected.Count == 0);
            if (isEmpty)
            {
                if (question.IsRequired) throw new ArgumentException($"Please answer \"{Shorten(question.Text)}\".");
                continue;
            }
            rows.Add(new
            {
                Id = Guid.NewGuid().ToString(),
                RequestId = request.Id,
                QuestionId = question.Id,
                TextValue = text,
                SelectedOptions = selected is null ? null : JsonSerializer.Serialize(selected),
            });
        }

        // Store the signature, then record everything in one go. A second submission racing on the same link
        // loses the conditional update, and its signature is removed.
        await using var signatureStream = new MemoryStream(signature);
        var signatureFile = new FormFile(signatureStream, 0, signature.Length, "signature", "signature.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg",
        };
        var signaturePath = await fileStorage.StoreAsync(signatureFile, "reference-signatures");
        try
        {
            if (db.Connection.State != System.Data.ConnectionState.Open) db.Connection.Open();
            using var transaction = db.Connection.BeginTransaction();
            var claimed = await db.StatementAsync(
                """
                UPDATE ReferenceRequests
                SET Status = 'submitted', SubmittedAt = @Now, SubmittedById = @UserId, SignerName = @SignerName, SignaturePath = @Path
                WHERE Id = @Id AND Status = 'pending'
                """,
                new { Id = request.Id, Now = DateTime.UtcNow, UserId = userId, SignerName = signerName, Path = signaturePath }, transaction);
            if (claimed == 0) throw new ArgumentException("This reference has already been submitted.");
            foreach (var row in rows) await db.Query("ReferenceAnswers").InsertAsync(row, transaction);
            transaction.Commit();
        }
        catch
        {
            await TryDeleteStoredAsync(signaturePath);
            throw;
        }

        await NotifyApplicantAsync(app, request, cycle);
    }

    // ── Scorer ───────────────────────────────────────────────────────────────

    public async Task<List<SubmittedReferenceDto>> ListSubmittedAsync(string applicationId, string userId, string role)
    {
        await EnsureScoringAccessAsync(applicationId, userId, role);

        var requests = (await db.Query("ReferenceRequests as r")
            .Join("ReferenceForms as f", "f.Id", "r.FormId")
            .Where("r.ApplicationId", applicationId).Where("r.Status", "submitted")
            .OrderBy("f.Order", "f.CreatedAt", "r.SubmittedAt")
            .Select("r.*")
            .GetAsync<ReferenceRequest>()).ToList();
        return await BuildSubmittedAsync(requests);
    }

    // ── Referee: the references they submitted (answers, never scores) ───────

    public async Task<List<SubmittedReferenceDto>> ListMineAsync(string userId)
    {
        var requests = (await db.Query("ReferenceRequests").Where("SubmittedById", userId).Where("Status", "submitted")
            .OrderByDesc("SubmittedAt").GetAsync<ReferenceRequest>()).ToList();
        return await BuildSubmittedAsync(requests);
    }

    public async Task<SubmittedReferenceDto> GetMineAsync(string requestId, string userId)
    {
        var request = await db.Query("ReferenceRequests").Where("Id", requestId).Where("SubmittedById", userId)
            .Where("Status", "submitted").FirstOrDefaultAsync<ReferenceRequest>()
            ?? throw new KeyNotFoundException("Reference not found.");
        return (await BuildSubmittedAsync([request])).Single();
    }

    // ── Signature and files: the referee who wrote it, or someone who can score it ──

    public async Task<(Stream Stream, string ContentType, string FileName)> GetSignatureAsync(string requestId, string userId, string role)
    {
        var request = await db.Query("ReferenceRequests").Where("Id", requestId).FirstOrDefaultAsync<ReferenceRequest>();
        if (request?.SignaturePath is null) throw new KeyNotFoundException("Signature not found.");
        await EnsureCanReadSubmittedAsync(request, userId, role);
        var (stream, _, _) = await fileStorage.RetrieveAsync(request.SignaturePath, "signature.jpg");
        return (stream, "image/jpeg", "signature.jpg");
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> GetFileAsync(string fileId, string userId, string role)
    {
        var file = await db.Query("ReferenceFiles").Where("Id", fileId).FirstOrDefaultAsync<ReferenceFile>()
            ?? throw new KeyNotFoundException("File not found.");
        var request = await db.Query("ReferenceRequests").Where("Id", file.RequestId).FirstAsync<ReferenceRequest>();
        if (request.Status != "submitted") throw new KeyNotFoundException("File not found.");
        // Only files that are part of the submitted reference. Anyone holding the code can upload while it's open,
        // so a file someone else uploaded but never attached isn't the submitter's (or anyone's) to read.
        var attached = await db.Query("ReferenceAnswers").Where("RequestId", request.Id).Where("TextValue", file.Id).ExistsAsync();
        if (!attached) throw new KeyNotFoundException("File not found.");
        await EnsureCanReadSubmittedAsync(request, userId, role);
        return await fileStorage.RetrieveAsync(file.StoragePath, file.FileName);
    }

    // Submitted references with their answers, in the order given
    private async Task<List<SubmittedReferenceDto>> BuildSubmittedAsync(List<ReferenceRequest> requests)
    {
        if (requests.Count == 0) return [];

        var forms = (await db.Query("ReferenceForms").WhereIn("Id", requests.Select(r => r.FormId).Distinct())
            .GetAsync<ReferenceForm>()).ToDictionary(f => f.Id);
        var questions = (await db.Query("Questions").WhereIn("ReferenceFormId", forms.Keys).OrderBy("Order")
            .GetAsync<Question>()).ToLookup(q => q.ReferenceFormId!);
        var allQuestionIds = questions.SelectMany(g => g).Select(q => q.Id).ToList();
        var options = allQuestionIds.Count == 0
            ? new Dictionary<string, string>()
            : (await db.Query("QuestionOptions").WhereIn("QuestionId", allQuestionIds).GetAsync<QuestionOption>())
                .ToDictionary(o => o.Id, o => o.Text);
        var requestIds = requests.Select(r => r.Id).ToList();
        var answers = (await db.Query("ReferenceAnswers").WhereIn("RequestId", requestIds).GetAsync<ReferenceAnswer>())
            .ToDictionary(a => (a.RequestId, a.QuestionId));
        var files = (await db.Query("ReferenceFiles").WhereIn("RequestId", requestIds).GetAsync<ReferenceFile>())
            .ToDictionary(f => f.Id);

        // Who it's for (applicant, school, cycle) and which account submitted it
        var apps = (await db.Query("Applications").WhereIn("Id", requests.Select(r => r.ApplicationId).Distinct())
            .GetAsync<Application>()).ToDictionary(a => a.Id);
        var cycles = (await db.Query("ScholarshipCycles").WhereIn("Id", apps.Values.Select(a => a.CycleId).Distinct())
            .GetAsync<ScholarshipCycle>()).ToDictionary(c => c.Id);
        var userIds = apps.Values.Select(a => a.ApplicantId).Concat(requests.Select(r => r.SubmittedById).OfType<string>()).Distinct().ToList();
        var users = (await db.Query("Users").WhereIn("Id", userIds).GetAsync<User>()).ToDictionary(u => u.Id);
        var schools = new Dictionary<string, string?>();
        foreach (var app in apps.Values)
            schools[app.Id] = users.TryGetValue(app.ApplicantId, out var applicant) ? await GetApplicantSchoolAsync(app, applicant) : null;

        return requests.Select(r =>
        {
            var app = apps[r.ApplicationId];
            var applicant = users.GetValueOrDefault(app.ApplicantId);
            return new SubmittedReferenceDto
            {
                Id = r.Id,
                FormId = r.FormId,
                FormTitle = forms[r.FormId].Title,
                Label = r.Label,
                ApplicantName = applicant is null ? "" : $"{applicant.FirstName} {applicant.LastName}".Trim(),
                SchoolName = schools[app.Id],
                CycleName = cycles.TryGetValue(app.CycleId, out var cycle) ? cycle.Name : "",
                SignerName = r.SignerName,
                SubmittedByEmail = r.SubmittedById is not null && users.TryGetValue(r.SubmittedById, out var submitter) ? submitter.Email : null,
                SubmittedAt = r.SubmittedAt ?? r.CreatedAt,
                HasSignature = r.SignaturePath is not null,
                Answers = questions[r.FormId].Select(q => ToAnswerDto(q, answers.GetValueOrDefault((r.Id, q.Id)), options, files)).ToList(),
            };
        }).ToList();
    }

    private static SubmittedReferenceAnswerDto ToAnswerDto(
        Question q, ReferenceAnswer? a, Dictionary<string, string> options, Dictionary<string, ReferenceFile> files)
    {
        var dto = new SubmittedReferenceAnswerDto
        {
            QuestionId = q.Id,
            QuestionText = q.Text,
            Type = q.Type,
            RichText = QuestionRules.IsRichText(q),
        };
        if (a is null) return dto;
        switch (q.Type)
        {
            case "single_choice":
                dto.SelectedOptions = a.TextValue is not null && options.TryGetValue(a.TextValue, out var text) ? [text] : [];
                break;
            case "multiple_choice":
                dto.SelectedOptions = (JsonSerializer.Deserialize<List<string>>(a.SelectedOptions ?? "[]") ?? [])
                    .Select(id => options.GetValueOrDefault(id)).OfType<string>().ToList();
                break;
            case "file_upload" when a.TextValue is not null && files.TryGetValue(a.TextValue, out var file):
                dto.FileId = file.Id;
                dto.FileName = file.FileName;
                break;
            default:
                dto.TextValue = a.TextValue;
                break;
        }
        return dto;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private ReferenceRequestDto ToDto(ReferenceRequest r) => new()
    {
        Id = r.Id,
        FormId = r.FormId,
        Label = r.Label,
        Status = r.Status,
        Link = r.Status == "pending" ? LinkFor(r) : null,
        CreatedAt = r.CreatedAt,
        SubmittedAt = r.SubmittedAt,
    };

    private string? LinkFor(ReferenceRequest r)
    {
        try
        {
            var code = protector.Unprotect(r.CodeEncrypted, ReferenceCode.AssociatedData(r.Id));
            return $"{(config["App:BaseUrl"] ?? "http://localhost:5173").TrimEnd('/')}/r/{code}";
        }
        catch (CryptographicException)
        {
            // The encryption key changed; the applicant can delete this link and make a new one
            return null;
        }
    }

    private static string LinkStatus(ReferenceRequest request, ScholarshipCycle cycle) =>
        request.Status == "submitted" ? "submitted" : CycleWindow.IsClosed(cycle, DateTime.UtcNow) ? "closed" : "open";

    private async Task<(ReferenceRequest Request, Application App, ScholarshipCycle Cycle)> FindLinkAsync(string? code, string userId)
    {
        const string invalid = "That reference code isn't valid. Check that all 10 characters were entered.";
        var normalized = ReferenceCode.Normalize(code) ?? throw new KeyNotFoundException(invalid);
        var request = await db.Query("ReferenceRequests").Where("CodeHash", ReferenceCode.Hash(normalized))
            .FirstOrDefaultAsync<ReferenceRequest>() ?? throw new KeyNotFoundException(invalid);
        var app = await GetApplicationAsync(request.ApplicationId);
        if (app.ApplicantId == userId) throw new UnauthorizedAccessException("You can't write a reference for your own application.");
        return (request, app, await GetCycleAsync(app.CycleId));
    }

    private async Task<(ReferenceRequest Request, Application App, ScholarshipCycle Cycle)> OpenLinkAsync(string? code, string userId)
    {
        var link = await FindLinkAsync(code, userId);
        switch (LinkStatus(link.Request, link.Cycle))
        {
            case "submitted": throw new ArgumentException("This reference has already been submitted.");
            case "closed": throw new ArgumentException("This reference link has expired because the scholarship deadline has passed.");
        }
        return link;
    }

    private async Task<Application> GetApplicationAsync(string applicationId) =>
        await db.Query("Applications").Where("Id", applicationId).FirstOrDefaultAsync<Application>()
            ?? throw new KeyNotFoundException("Application not found.");

    private async Task<ScholarshipCycle> GetCycleAsync(string cycleId) =>
        await db.Query("ScholarshipCycles").Where("Id", cycleId).FirstAsync<ScholarshipCycle>();

    // Links can only be made or deleted while the application can still be edited
    private async Task<Application> GetEditableApplicationAsync(string applicationId, string applicantId)
    {
        var app = await GetApplicationAsync(applicationId);
        if (app.ApplicantId != applicantId) throw new UnauthorizedAccessException("Access denied.");
        if (app.Status != "draft") throw new ArgumentException("References can't be changed after the application is submitted.");
        if (!CycleWindow.IsOpen(await GetCycleAsync(app.CycleId), DateTime.UtcNow))
            throw new ArgumentException("This scholarship cycle has closed.");
        return app;
    }

    private async Task EnsureCanReadSubmittedAsync(ReferenceRequest request, string userId, string role)
    {
        if (request.Status == "submitted" && request.SubmittedById == userId) return;
        await EnsureScoringAccessAsync(request.ApplicationId, userId, role);
    }

    // Reference answers are for scoring only: scorers (once the application is scoreable) and admins, never the applicant
    private async Task EnsureScoringAccessAsync(string applicationId, string userId, string role)
    {
        if (!(role == "scorer" || AppRoles.IsAdmin(role))) throw new UnauthorizedAccessException("Access denied.");
        var app = await GetApplicationAsync(applicationId);
        if (!await access.CanViewAsync(app.Id, app.ApplicantId, app.Status, app.CycleId, userId, role))
            throw new UnauthorizedAccessException("Access denied.");
    }

    // The school chosen on the application, else the one on the applicant's account
    private async Task<string?> GetApplicantSchoolAsync(Application app, User applicant)
    {
        var schoolId = await db.Query("ApplicationAnswers as aa")
            .Join("Questions as q", "q.Id", "aa.QuestionId")
            .Where("aa.ApplicationId", app.Id).Where("q.Type", "school_select").WhereNull("q.ReferenceFormId")
            .WhereNotNull("aa.TextValue").Where("aa.TextValue", "<>", "")
            .OrderBy("q.Order").Select("aa.TextValue").FirstOrDefaultAsync<string>()
            ?? applicant.SchoolId;
        return string.IsNullOrEmpty(schoolId)
            ? null
            : await db.Query("Schools").Where("Id", schoolId).Select("Name").FirstOrDefaultAsync<string>();
    }

    private async Task<List<ReferenceQuestionDto>> LoadFormQuestionsAsync(string formId)
    {
        var questions = (await db.Query("Questions").Where("ReferenceFormId", formId).OrderBy("Order").GetAsync<Question>()).ToList();
        if (questions.Count == 0) return [];
        var options = (await db.Query("QuestionOptions").WhereIn("QuestionId", questions.Select(q => q.Id)).OrderBy("Order")
            .GetAsync<QuestionOption>()).ToLookup(o => o.QuestionId);
        return questions.Select(q => new ReferenceQuestionDto
        {
            Id = q.Id,
            Text = q.Text,
            Type = q.Type,
            IsRequired = q.IsRequired,
            Description = q.Description,
            ValidationRules = q.ValidationRules is null ? null : JsonSerializer.Deserialize<JsonElement>(q.ValidationRules),
            Options = options[q.Id].Select(o => new ReferenceOptionDto { Id = o.Id, Text = o.Text, Order = o.Order }).ToList(),
        }).ToList();
    }

    // Checks one answer against its question and returns what to store (text, or selected option ids)
    private static (string? Text, List<string>? Selected) NormalizeAnswer(
        Question question, AnswerDto? answer, List<QuestionOption> options, List<ReferenceFile> files)
    {
        if (answer is null) return (null, null);
        var label = Shorten(question.Text);
        var text = string.IsNullOrWhiteSpace(answer.TextValue) ? null : answer.TextValue.Trim();

        switch (question.Type)
        {
            case "single_choice":
                if (text is not null && options.All(o => o.Id != text))
                    throw new ArgumentException($"Choose one of the listed answers for \"{label}\".");
                return (text, null);

            case "multiple_choice":
                var selected = (answer.SelectedOptions ?? []).Distinct().ToList();
                if (selected.Any(id => options.All(o => o.Id != id)))
                    throw new ArgumentException($"Choose from the listed answers for \"{label}\".");
                return (null, selected.Count == 0 ? null : selected);

            case "file_upload":
                if (text is not null && !files.Any(f => f.Id == text && f.QuestionId == question.Id))
                    throw new ArgumentException($"The file for \"{label}\" couldn't be found. Please upload it again.");
                return (text, null);

            case "date":
                if (text is not null && !DateOnly.TryParseExact(text, "yyyy-MM-dd", out _))
                    throw new ArgumentException($"Enter a valid date for \"{label}\".");
                return (text, null);

            default:
                if (text is not null && QuestionRules.IsRichText(question)) text = RichTextSanitizer.Sanitize(text);
                if (text?.Length > MaxAnswerLength)
                    throw new ArgumentException($"The answer to \"{label}\" must be at most {MaxAnswerLength} characters.");
                return (text, null);
        }
    }

    private static byte[] DecodeSignature(string? dataUrl)
    {
        const string missing = "Please sign in the signature box.";
        if (string.IsNullOrEmpty(dataUrl) || !dataUrl.StartsWith(SignatureDataUrlPrefix, StringComparison.Ordinal))
            throw new ArgumentException(missing);

        byte[] bytes;
        try { bytes = Convert.FromBase64String(dataUrl[SignatureDataUrlPrefix.Length..]); }
        catch (FormatException) { throw new ArgumentException(missing); }

        // JPEG files start with the SOI marker FF D8 FF
        if (bytes.Length is < 3 or > MaxSignatureBytes || bytes[0] != 0xFF || bytes[1] != 0xD8 || bytes[2] != 0xFF)
            throw new ArgumentException("The signature couldn't be read. Please clear it and sign again.");
        return bytes;
    }

    private static string Shorten(string text) => text.Length <= 60 ? text : text[..57] + "…";

    private async Task TryDeleteStoredAsync(string storagePath)
    {
        try { await fileStorage.DeleteAsync(storagePath); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to delete stored reference file {StoragePath}", storagePath); }
    }

    // Tells the applicant a reference arrived, without any of its content
    private async Task NotifyApplicantAsync(Application app, ReferenceRequest request, ScholarshipCycle cycle)
    {
        try
        {
            var applicant = await db.Query("Users").Where("Id", app.ApplicantId).FirstAsync<User>();
            var form = await db.Query("ReferenceForms").Where("Id", request.FormId).FirstAsync<ReferenceForm>();
            var what = string.IsNullOrEmpty(request.Label) ? form.Title : $"{form.Title} ({request.Label})";
            var link = $"{(config["App:BaseUrl"] ?? "http://localhost:5173").TrimEnd('/')}/application/{app.Id}";

            var html = $"""
                <div style="font-family:Arial,sans-serif;max-width:480px;margin:0 auto;color:#2B3674">
                  <h2 style="margin-bottom:8px">A reference was submitted</h2>
                  <p>Hi {WebUtility.HtmlEncode(applicant.FirstName)},</p>
                  <p>Your <strong>{WebUtility.HtmlEncode(what)}</strong> for {WebUtility.HtmlEncode(cycle.Name)} was just submitted.
                     References go straight to the scholarship committee, so you won't see what was written.</p>
                  <p style="margin:28px 0">
                    <a href="{link}" style="background:#4361EE;color:#fff;padding:12px 24px;border-radius:12px;text-decoration:none;font-weight:bold">View your application</a>
                  </p>
                </div>
                """;
            var text = $"Hi {applicant.FirstName},\n\nYour {what} for {cycle.Name} was just submitted. References go straight to the scholarship committee, so you won't see what was written.\n\nView your application: {link}";
            await email.SendAsync(applicant.Email, "A reference was submitted for your application", html, text);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send reference notification for request {RequestId}", request.Id);
        }
    }
}
