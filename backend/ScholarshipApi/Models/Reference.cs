namespace ScholarshipApi.Models;

public class Reference
{
    public string Id { get; set; } = null!;
    public string ApplicationId { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string? Label { get; set; }
    public string Status { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
