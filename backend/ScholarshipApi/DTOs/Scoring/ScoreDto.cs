namespace ScholarshipApi.DTOs.Scoring;

public class ScoreDto
{
    public string Id { get; set; } = null!;
    public string ApplicationId { get; set; } = null!;
    public string ScoredById { get; set; } = null!;
    public string ScorerName { get; set; } = null!;
    public string? SectionId { get; set; }
    public string? SectionTitle { get; set; }
    public decimal ScoreValue { get; set; }
    public string? Comments { get; set; }
    public DateTime ScoredAt { get; set; }
}
