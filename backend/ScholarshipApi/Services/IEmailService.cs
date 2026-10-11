namespace ScholarshipApi.Services;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string html, string text);
}
