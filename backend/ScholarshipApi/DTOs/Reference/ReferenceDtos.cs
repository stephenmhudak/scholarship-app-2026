using System.Text.Json;
using ScholarshipApi.DTOs.Application;

namespace ScholarshipApi.DTOs.Reference;

// ── Applicant: status only, never the referee's answers ─────────────────────

public class ApplicationReferencesDto
{
    /// <summary>False once the application is submitted or its cycle has closed.</summary>
    public bool CanRequest { get; set; }
    public List<ReferenceFormStatusDto> Forms { get; set; } = [];
}

public class ReferenceFormStatusDto
{
    public string FormId { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public int RequiredCount { get; set; }
    /// <summary>False for a form that's no longer required by the saved answers but already has links.</summary>
    public bool Required { get; set; }
    public int Received { get; set; }
    public List<ReferenceRequestDto> Requests { get; set; } = [];
}

public class ReferenceRequestDto
{
    public string Id { get; set; } = null!;
    public string FormId { get; set; } = null!;
    public string? Label { get; set; }
    public string Status { get; set; } = null!;
    /// <summary>Only while pending; the link stops working once the reference is submitted.</summary>
    public string? Link { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
}

public class CreateReferenceRequestBody
{
    public string FormId { get; set; } = null!;
    public string? Label { get; set; }
}

// ── Referee (public link) ──────────────────────────────────────────────────

public class ReferenceLinkDto
{
    /// <summary><c>open</c>, <c>submitted</c> or <c>closed</c>; questions are only included while open.</summary>
    public string Status { get; set; } = null!;
    public string ApplicantName { get; set; } = null!;
    public string? SchoolName { get; set; }
    public string CycleName { get; set; } = null!;
    public string FormTitle { get; set; } = null!;
    public string? FormDescription { get; set; }
    public List<ReferenceQuestionDto> Questions { get; set; } = [];
}

public class ReferenceQuestionDto
{
    public string Id { get; set; } = null!;
    public string Text { get; set; } = null!;
    public string Type { get; set; } = null!;
    public bool IsRequired { get; set; }
    public string? Description { get; set; }
    public JsonElement? ValidationRules { get; set; }
    public List<ReferenceOptionDto> Options { get; set; } = [];
}

public class ReferenceOptionDto
{
    public string Id { get; set; } = null!;
    public string Text { get; set; } = null!;
    public int Order { get; set; }
}

public class ReferenceSubmission
{
    public string? SignerName { get; set; }
    /// <summary>The drawn signature as a <c>data:image/jpeg;base64,…</c> URL.</summary>
    public string? Signature { get; set; }
    public List<AnswerDto> Answers { get; set; } = [];
}

// ── Scorer ─────────────────────────────────────────────────────────────────

public class SubmittedReferenceDto
{
    public string Id { get; set; } = null!;
    public string FormId { get; set; } = null!;
    public string FormTitle { get; set; } = null!;
    public string? Label { get; set; }
    public string? SignerName { get; set; }
    public DateTime SubmittedAt { get; set; }
    public bool HasSignature { get; set; }
    public List<SubmittedReferenceAnswerDto> Answers { get; set; } = [];
}

public class SubmittedReferenceAnswerDto
{
    public string QuestionId { get; set; } = null!;
    public string QuestionText { get; set; } = null!;
    public string Type { get; set; } = null!;
    public bool RichText { get; set; }
    /// <summary>Text answers and dates; choice answers come as <see cref="SelectedOptions"/> (option text).</summary>
    public string? TextValue { get; set; }
    public List<string>? SelectedOptions { get; set; }
    public string? FileId { get; set; }
    public string? FileName { get; set; }
}
