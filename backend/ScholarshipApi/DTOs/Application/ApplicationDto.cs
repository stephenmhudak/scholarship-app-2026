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
    public List<AnswerDto> Answers { get; set; } = [];
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
