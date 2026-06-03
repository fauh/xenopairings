using Xenopairings.Models;

namespace Xenopairings.Services.Auth;

public interface IAuthService
{
    /// <summary>
    /// Creates a new user. Throws <see cref="InvalidOperationException"/> if the
    /// email is already registered.
    /// </summary>
    Task<User> RegisterAsync(string email, string password);

    /// <summary>
    /// Returns the user if the email + password combination is valid, null otherwise.
    /// </summary>
    Task<User?> LoginAsync(string email, string password);

    Task<User?> GetByEmailAsync(string email);
    Task<IReadOnlyList<User>> ListAllAsync();
    Task SetVipAsync(Guid userId, bool isVip);

    // ── Email verification ────────────────────────────────────────────────────
    /// <summary>Generates a verification token and sends the confirmation email.</summary>
    Task SendVerificationEmailAsync(Guid userId, string baseUrl);
    /// <summary>Marks the user's email as verified. Returns false if the token is invalid/expired.</summary>
    Task<bool> VerifyEmailAsync(string token);

    // ── Password reset ────────────────────────────────────────────────────────
    /// <summary>If the email exists, generates a reset token and sends the email. Always returns (no leak).</summary>
    Task SendPasswordResetAsync(string email, string baseUrl);
    /// <summary>Resets the password using the token. Returns false if token is invalid or expired.</summary>
    Task<bool> ResetPasswordAsync(string token, string newPassword);
}
