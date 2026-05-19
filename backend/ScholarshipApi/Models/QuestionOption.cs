namespace ScholarshipApi.Models;

public class QuestionOption
{
    public string Id { get; set; } = null!;
    public string QuestionId { get; set; } = null!;
    public string Text { get; set; } = null!;
    public int Order { get; set; }
}
