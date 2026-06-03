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
}
