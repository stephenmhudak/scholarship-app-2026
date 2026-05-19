namespace ScholarshipApi.Models;

public class ScholarshipCycle
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public DateTime OpenDate { get; set; }
    public DateTime CloseDate { get; set; }
    public bool IsActive { get; set; }
}
