namespace ScholarshipApi.Models;

public class Section
{
    public string Id { get; set; } = null!;
    public string CycleId { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public int Order { get; set; }
    /// <summary><c>eligibility</c> for the cycle's shared scholarship eligibility section, otherwise null.</summary>
    public string? Kind { get; set; }
    /// <summary>Set on a scholarship's own section: its questions are only shown to eligible applicants.</summary>
    public string? ScholarshipId { get; set; }
}
