namespace ScholarshipApi.DTOs.Auth;

public class RegisterRequest
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    /// <summary><c>applicant</c> (default) or <c>referee</c>; ignored with an invite, which makes a school admin.</summary>
    public string? AccountType { get; set; }
    public string? InviteToken { get; set; }
    public string? SchoolName { get; set; }
    public string? SchoolAddressLine1 { get; set; }
    public string? SchoolAddressLine2 { get; set; }
    public string? SchoolCity { get; set; }
    public string? SchoolState { get; set; }
    public string? SchoolZip { get; set; }
}
