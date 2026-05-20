namespace ScholarshipApi.Models;

public class Question
{
    public string Id { get; set; } = null!;
    public string CycleId { get; set; } = null!;
    public string Text { get; set; } = null!;
    public string Type { get; set; } = null!;
    public int Order { get; set; }
    public bool IsRequired { get; set; }
    public string? SectionId { get; set; }
    public string? ValidationRules { get; set; }
}
