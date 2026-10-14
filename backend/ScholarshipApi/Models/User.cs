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
    public string? TwoFactorMethod { get; set; }
    public string? EmailOtpSetupHash { get; set; }
    public DateTime? EmailOtpSetupExpiresAt { get; set; }
    public int SessionVersion { get; set; }
    /// <summary>Scorers: <c>all</c>, or <c>filtered</c> to applications matching their <see cref="ScorerFilter"/>s.</summary>
    public string ScoringScope { get; set; } = "all";
    public DateTime CreatedAt { get; set; }
}

public class ScorerFilter
{
    public string Id { get; set; } = null!;
    public string ScorerId { get; set; } = null!;
    public string QuestionId { get; set; } = null!;
    public string? OptionId { get; set; }
    public string? TextContains { get; set; }
}
