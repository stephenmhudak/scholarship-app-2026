namespace ScholarshipApi.Models;

public class ReferenceForm
{
    public string Id { get; set; } = null!;
    public string CycleId { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public int RequiredCount { get; set; }
    /// <summary><c>always</c> or <c>conditional</c> (see <see cref="ReferenceFormCondition"/>).</summary>
    public string Requirement { get; set; } = null!;
    public int Order { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ReferenceFormCondition
{
    public string Id { get; set; } = null!;
    public string FormId { get; set; } = null!;
    public string QuestionId { get; set; } = null!;
    public string? OptionId { get; set; }
    public string? TextContains { get; set; }
}

public class ReferenceRequest
{
    public string Id { get; set; } = null!;
    public string ApplicationId { get; set; } = null!;
    public string FormId { get; set; } = null!;
    public string? Label { get; set; }
    public string CodeHash { get; set; } = null!;
    public string CodeEncrypted { get; set; } = null!;
    /// <summary><c>pending</c> until the referee submits, then <c>submitted</c>.</summary>
    public string Status { get; set; } = null!;
    public string? SignerName { get; set; }
    public string? SignaturePath { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ReferenceAnswer
{
    public string Id { get; set; } = null!;
    public string RequestId { get; set; } = null!;
    public string QuestionId { get; set; } = null!;
    public string? TextValue { get; set; }
    public string? SelectedOptions { get; set; }
}

public class ReferenceFile
{
    public string Id { get; set; } = null!;
    public string RequestId { get; set; } = null!;
    public string? QuestionId { get; set; }
    public string FileName { get; set; } = null!;
    public string StoragePath { get; set; } = null!;
    public DateTime UploadedAt { get; set; }
}
