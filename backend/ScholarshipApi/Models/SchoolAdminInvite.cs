namespace ScholarshipApi.Models;

public class SchoolAdminInvite
{
    public string Id { get; set; } = null!;
    public string SchoolId { get; set; } = null!;
    public string Token { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
}
