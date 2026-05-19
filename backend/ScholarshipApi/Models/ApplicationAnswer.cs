namespace ScholarshipApi.Models;

public class ApplicationAnswer
{
    public string Id { get; set; } = null!;
    public string ApplicationId { get; set; } = null!;
    public string QuestionId { get; set; } = null!;
    public string? TextValue { get; set; }
    public string? SelectedOptions { get; set; }
}
