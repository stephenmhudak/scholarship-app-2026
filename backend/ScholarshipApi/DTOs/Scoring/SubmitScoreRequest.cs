namespace ScholarshipApi.DTOs.Scoring;

public class SectionScoreInput
{
    public string? SectionId { get; set; }
    public decimal Score { get; set; }
    public string? Comments { get; set; }
}

public class SubmitScoreRequest
{
    public List<SectionScoreInput> SectionScores { get; set; } = [];
}
