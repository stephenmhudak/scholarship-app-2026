namespace ScholarshipApi.Models;

public class Section
{
    public string Id { get; set; } = null!;
    public string CycleId { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public int Order { get; set; }
}
