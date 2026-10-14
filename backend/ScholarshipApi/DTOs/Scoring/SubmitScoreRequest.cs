namespace ScholarshipApi.DTOs.Scoring;

public class SectionScoreInput
{
    public string? SectionId { get; set; }
    /// <summary>Set (instead of SectionId) to score one submitted reference.</summary>
    public string? ReferenceRequestId { get; set; }
    public decimal Score { get; set; }
    public string? Comments { get; set; }
}

public class SubmitScoreRequest
{
    public List<SectionScoreInput> SectionScores { get; set; } = [];
}
