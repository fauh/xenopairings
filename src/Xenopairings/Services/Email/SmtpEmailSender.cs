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
        // 15 second connect timeout — fail fast rather than hanging indefinitely
        client.Timeout = 15_000;
        try
        {
            // Port 465 → implicit SSL; anything else → STARTTLS
            var socketOptions = cfg.SmtpPort == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;

            await client.ConnectAsync(cfg.SmtpHost, cfg.SmtpPort, socketOptions);
            await client.AuthenticateAsync(cfg.SmtpUsername, cfg.SmtpPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
            logger.LogInformation("Email sent via SMTP to {To}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SMTP send failed ({Host}:{Port}) to {To}: {Subject}",
                cfg.SmtpHost, cfg.SmtpPort, to, subject);
            throw;
        }
    }
}
