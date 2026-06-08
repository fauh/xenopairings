namespace Xenopairings.Services.Email;

public sealed class EmailSettings
{
    public const string SectionName = "EmailSettings";

    /// <summary>Absolute base URL prepended to all relative paths in email bodies.</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>From address used in outbound emails, e.g. noreply@xenopairings.com</summary>
    public string FromAddress { get; init; } = string.Empty;

    /// <summary>Optional display name shown in the From field, e.g. "Xenopairings"</summary>
    public string FromName { get; init; } = "Xenopairings";

    /// <summary>
    /// Which provider to use: "console" (default dev), "smtp" (Brevo/any), "resend".
    /// Overrides UseRealProvider when set.
    /// </summary>
    public string Provider { get; init; } = "console";

    // ── SMTP settings (used when Provider = "smtp") ────────────────────────────
    public string SmtpHost     { get; init; } = string.Empty;
    public int    SmtpPort     { get; init; } = 587;
    public string SmtpUsername { get; init; } = string.Empty;
    public string SmtpPassword { get; init; } = string.Empty;

    // ── Resend settings (used when Provider = "resend") ───────────────────────
    /// <summary>Resend API key.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Legacy flag — kept for backward compat. Use Provider instead.</summary>
    public bool UseRealProvider { get; init; } = false;
}
