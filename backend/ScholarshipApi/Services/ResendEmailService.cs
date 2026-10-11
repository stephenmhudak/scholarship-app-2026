using System.Net.Http.Headers;

namespace ScholarshipApi.Services;

public class ResendEmailService(HttpClient http, IConfiguration config, ILogger<ResendEmailService> logger) : IEmailService
{
    public async Task SendAsync(string to, string subject, string html, string text)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = JsonContent.Create(new
            {
                from = config["Email:From"],
                to = new[] { to },
                subject,
                html,
                text
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config["Email:Resend:ApiKey"]);

        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            logger.LogError("Resend returned {Status}: {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException("Failed to send email.");
        }
    }
}
