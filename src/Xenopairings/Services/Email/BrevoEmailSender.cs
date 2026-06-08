using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Xenopairings.Services.Email;

/// <summary>
/// Brevo (formerly Sendinblue) transactional email sender using their HTTP API.
/// No SMTP — works on Railway and any cloud platform.
///
/// Setup:
///   1. Create a free Brevo account at brevo.com
///   2. Settings → SMTP &amp; API → API Keys → Generate a new API key
///   3. Set EmailSettings__Provider = "brevo" and EmailSettings__ApiKey = &lt;your key&gt;
///   4. Verify your sender email in Brevo → Senders &amp; Domains
/// </summary>
public sealed class BrevoEmailSender(
    HttpClient http,
    IOptions<EmailSettings> settings,
    ILogger<BrevoEmailSender> logger) : IEmailSender
{
    private const string ApiUrl = "https://api.brevo.com/v3/smtp/email";

    public async Task SendAsync(string to, string subject, string body)
    {
        var cfg = settings.Value;

        var payload = JsonSerializer.Serialize(new
        {
            sender  = new { name = cfg.FromName, email = cfg.FromAddress },
            to      = new[] { new { email = to } },
            subject,
            textContent = body,
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
        request.Headers.Add("api-key", cfg.ApiKey);
        request.Headers.Accept.ParseAdd("application/json");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        try
        {
            var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                logger.LogError("Brevo API error {Status} sending to {To}: {Error}",
                    (int)response.StatusCode, to, error);
                response.EnsureSuccessStatusCode();
            }
            logger.LogInformation("Email sent via Brevo to {To}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Brevo send failed to {To}: {Subject}", to, subject);
            throw;
        }
    }
}
