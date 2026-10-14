namespace ScholarshipApi.DTOs.Auth;

public class PasswordSetupCheckRequest
{
    public string? Token { get; set; }
}

public class PasswordSetupRequest
{
    public string? Token { get; set; }
    public string? NewPassword { get; set; }
}

/// <summary>Who the setup link is for, so the page can greet them.</summary>
public class PasswordSetupInfoResponse
{
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
}
