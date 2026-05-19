namespace ScholarshipApi.DTOs.Application;

public class AnswerDto
{
    public string QuestionId { get; set; } = null!;
    public string? TextValue { get; set; }
    public List<string>? SelectedOptions { get; set; }
}
