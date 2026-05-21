namespace ScholarshipApi.DTOs.Auth;

public class InviteInfoResponse
{
    public string SchoolId { get; set; } = null!;
    public string SchoolName { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
}
