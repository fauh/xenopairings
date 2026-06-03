namespace Xenopairings.Services.Auth;

public sealed class AdminSettings
{
    public const string SectionName = "AdminSettings";

    /// <summary>
    /// Emails that receive the Admin role on login.
    /// Checked case-insensitively. Add more entries here when needed.
    /// </summary>
    public IReadOnlyList<string> AdminEmails { get; init; } = [];

    public bool IsAdmin(string? email) =>
        email is not null &&
        AdminEmails.Any(a => a.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase));
}
