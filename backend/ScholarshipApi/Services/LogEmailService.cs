namespace ScholarshipApi.Services;

// Dev fallback when no Resend API key is configured: writes emails to the console instead of sending
public class LogEmailService(ILogger<LogEmailService> logger) : IEmailService
{
    public Task SendAsync(string to, string subject, string html, string text)
    {
        logger.LogInformation("Email to {To} — {Subject}\n{Text}", to, subject, text);
        return Task.CompletedTask;
    }
}
