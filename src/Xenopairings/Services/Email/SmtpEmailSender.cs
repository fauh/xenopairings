using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Xenopairings.Services.Email;

/// <summary>
/// SMTP email sender using MailKit. Works with any SMTP relay including Brevo.
///
/// Brevo SMTP settings:
///   Host:     smtp-relay.brevo.com
///   Port:     587
///   Username: your Brevo account email
///   Password: your Brevo SMTP API key (Settings → SMTP &amp; API)
/// </summary>
public sealed class SmtpEmailSender(
    IOptions<EmailSettings> settings,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body)
    {
        var cfg = settings.Value;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(cfg.FromName, cfg.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(cfg.SmtpHost, cfg.SmtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(cfg.SmtpUsername, cfg.SmtpPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
            logger.LogInformation("Email sent via SMTP to {To}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SMTP send failed to {To}: {Subject}", to, subject);
            throw;
        }
    }
}
