namespace ScholarshipApi.Models;

public class ApplicationScorer
{
    public string Id { get; set; } = null!;
    public string ApplicationId { get; set; } = null!;
    public string ScoredById { get; set; } = null!;
    public DateTime AssignedAt { get; set; }
}
