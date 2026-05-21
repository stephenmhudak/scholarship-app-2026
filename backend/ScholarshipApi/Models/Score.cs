namespace ScholarshipApi.Models;

public class Score
{
    public string Id { get; set; } = null!;
    public string ApplicationId { get; set; } = null!;
    public string ScoredById { get; set; } = null!;
    public string? SectionId { get; set; }
    public decimal ScoreValue { get; set; }
    public string? Comments { get; set; }
    public DateTime ScoredAt { get; set; }
}
