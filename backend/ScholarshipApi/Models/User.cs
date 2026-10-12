namespace ScholarshipApi.Models;

public class User
{
    public string Id { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Role { get; set; } = null!;
    public string? SchoolId { get; set; }
    public DateTime? EmailVerifiedAt { get; set; }
    public string? TotpSecret { get; set; }
    public string? TotpPendingSecret { get; set; }
    public DateTime? TotpEnabledAt { get; set; }
    public long? TotpLastUsedStep { get; set; }
    public DateTime CreatedAt { get; set; }
}
