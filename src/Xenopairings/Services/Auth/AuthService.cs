using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xenopairings.Data;
using Xenopairings.Models;

namespace Xenopairings.Services.Auth;

public sealed class AuthService(
    AppDbContext db,
    IPasswordHasher<User> passwordHasher) : IAuthService
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
}
