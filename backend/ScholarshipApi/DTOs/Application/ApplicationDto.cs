namespace ScholarshipApi.DTOs.Application;

public class ApplicationDto
{
    public string Id { get; set; } = null!;
    public string ApplicantId { get; set; } = null!;
    public string CycleId { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime? SubmittedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ApplicantFirstName { get; set; }
    public string? ApplicantLastName { get; set; }
    public string? ApplicantEmail { get; set; }
    public string? CycleName { get; set; }
    public DateTime? CycleCloseDate { get; set; }
    /// <summary>Whether the cycle is accepting applications now (see CycleWindow).</summary>
    public bool IsCycleOpen { get; set; }
    public List<AnswerDto> Answers { get; set; } = [];
}

public class OpenCycleDto
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public DateTime OpenDate { get; set; }
    public DateTime CloseDate { get; set; }
}

public class CopyAnswersResult
{
    /// <summary>Answers copied into unanswered questions.</summary>
    public int Copied { get; set; }
    /// <summary>Matching questions left alone because they were already answered.</summary>
    public int AlreadyAnswered { get; set; }
}

public class ApplicationListDto
{
    public string Id { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public DateTime? SubmittedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
