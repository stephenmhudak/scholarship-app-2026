namespace ScholarshipApi.Models;

public class Application
{
    public string Id { get; set; } = null!;
    public string ApplicantId { get; set; } = null!;
    public string CycleId { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime? SubmittedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
