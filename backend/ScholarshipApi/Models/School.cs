namespace ScholarshipApi.Models;

public class School
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Address { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
