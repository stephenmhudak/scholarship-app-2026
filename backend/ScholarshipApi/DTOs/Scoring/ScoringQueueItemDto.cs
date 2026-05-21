namespace ScholarshipApi.DTOs.Scoring;

public class ScoringQueueItemDto
{
    public string Id { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public DateTime? SubmittedAt { get; set; }
    public bool HasScored { get; set; }
}
