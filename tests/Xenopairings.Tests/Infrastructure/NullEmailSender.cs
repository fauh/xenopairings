using Xenopairings.Services.Email;

namespace Xenopairings.Tests.Infrastructure;

/// <summary>No-op email sender for tests — discards all messages silently.</summary>
public sealed class NullEmailSender : IEmailSender
{
    public Task SendAsync(string to, string subject, string body) => Task.CompletedTask;
}
