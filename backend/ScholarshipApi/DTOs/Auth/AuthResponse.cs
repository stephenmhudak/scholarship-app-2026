namespace ScholarshipApi.DTOs.Auth;

public class AuthResponse
{
    public string Token { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Role { get; set; } = null!;
    public string? SchoolId { get; set; }
    public string[] Permissions { get; set; } = [];
    public bool TwoFactorEnabled { get; set; }
    /// <summary>Set while a super admin is impersonating this user: the super admin's name, for the "end" button.</summary>
    public string? ImpersonatorName { get; set; }
    /// <summary>
    /// Only when an impersonation starts: a fresh session for the super admin, which the browser keeps so it can always
    /// return them to their own account, even if the impersonated session is rejected (expired, or reset).
    /// </summary>
    public AuthResponse? ReturnSession { get; set; }
}
