namespace ScholarshipApi.DTOs.Auth;

public class RegisterResponse
{
    public string Email { get; set; } = null!;
    public bool VerificationRequired { get; set; } = true;
}
