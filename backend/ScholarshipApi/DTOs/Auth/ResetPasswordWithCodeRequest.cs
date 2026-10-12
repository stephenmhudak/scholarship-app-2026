namespace ScholarshipApi.DTOs.Auth;

public class ResetPasswordWithCodeRequest
{
    public string Email { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string NewPassword { get; set; } = null!;
}
