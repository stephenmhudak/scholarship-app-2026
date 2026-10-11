namespace ScholarshipApi.Services;

public class EmailNotVerifiedException(string email)
    : Exception("Please verify your email address before signing in.")
{
    public string Email { get; } = email;
}
