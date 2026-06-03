using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xenopairings.Data;
using Xenopairings.Models;
using Xenopairings.Services.Email;

namespace Xenopairings.Services.Auth;

public sealed class AuthService(
    AppDbContext db,
    IPasswordHasher<User> passwordHasher,
    IEmailSender emailSender,
    IOptions<EmailSettings> emailSettings,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<User> RegisterAsync(string email, string password)
    {
        var normalised = email.Trim().ToLowerInvariant();

        var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == normalised);
        if (existing is not null)
            throw new InvalidOperationException($"An account with email '{normalised}' already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalised,
            EmailVerified = false,
            EmailVerificationToken = GenerateToken(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public async Task<User?> LoginAsync(string email, string password)
    {
        var normalised = email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalised);
        if (user is null) return null;

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result == PasswordVerificationResult.Failed ? null : user;
    }

    public Task<User?> GetByEmailAsync(string email)
    {
        var normalised = email.Trim().ToLowerInvariant();
        return db.Users.FirstOrDefaultAsync(u => u.Email == normalised);
    }

    // ── Email verification ────────────────────────────────────────────────────

    public async Task SendVerificationEmailAsync(Guid userId, string baseUrl)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null || user.EmailVerified) return;

        if (string.IsNullOrWhiteSpace(user.EmailVerificationToken))
        {
            user.EmailVerificationToken = GenerateToken();
            await db.SaveChangesAsync();
        }

        var link = $"{baseUrl.TrimEnd('/')}/verify-email?token={user.EmailVerificationToken}";
        try
        {
            await emailSender.SendAsync(user.Email,
                "Verify your Xenopairings email",
                $"""
                Hi,

                Please verify your email address by clicking the link below:
                {link}

                This link never expires. If you didn't create an account, ignore this email.
                """);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send verification email to {Email}.", user.Email);
        }
    }

    public async Task<bool> VerifyEmailAsync(string token)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.EmailVerificationToken == token);
        if (user is null) return false;
        user.EmailVerified = true;
        user.EmailVerificationToken = null;
        await db.SaveChangesAsync();
        return true;
    }

    // ── Password reset ────────────────────────────────────────────────────────

    public async Task SendPasswordResetAsync(string email, string baseUrl)
    {
        var normalised = email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalised);
        if (user is null) return;  // no leak — always succeed silently

        user.PasswordResetToken = GenerateToken();
        user.PasswordResetTokenExpiry = DateTimeOffset.UtcNow.AddHours(2);
        await db.SaveChangesAsync();

        var link = $"{baseUrl.TrimEnd('/')}/reset-password?token={user.PasswordResetToken}";
        try
        {
            await emailSender.SendAsync(user.Email,
                "Reset your Xenopairings password",
                $"""
                Hi,

                You requested a password reset. Click the link below (valid for 2 hours):
                {link}

                If you didn't request this, ignore this email — your password won't change.
                """);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send password reset email to {Email}.", normalised);
        }
    }

    public async Task<bool> ResetPasswordAsync(string token, string newPassword)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == token);
        if (user is null) return false;
        if (user.PasswordResetTokenExpiry < DateTimeOffset.UtcNow) return false;

        user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;
        await db.SaveChangesAsync();
        return true;
    }

    private static string GenerateToken() =>
        Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

    public async Task<IReadOnlyList<User>> ListAllAsync()
    {
        var users = await db.Users.ToListAsync();
        return [.. users.OrderBy(u => u.Email)];
    }

    public async Task SetVipAsync(Guid userId, bool isVip)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null) return;
        user.IsVip = isVip;

        // Keep PlayerRating.IsVip in sync
        var rating = await db.PlayerRatings
            .FirstOrDefaultAsync(r => r.Email == user.Email);
        if (rating is not null)
            rating.IsVip = isVip;

        await db.SaveChangesAsync();
    }
}
