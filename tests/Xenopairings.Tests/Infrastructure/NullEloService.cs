using Xenopairings.Models;
using Xenopairings.Services.Elo;

namespace Xenopairings.Tests.Infrastructure;

/// <summary>No-op ELO service for tests.</summary>
public sealed class NullEloService : IEloService
{
    public Task UpdateMatchRatingsAsync(Guid matchId) => Task.CompletedTask;
    public Task<IReadOnlyList<PlayerRating>> GetLeaderboardAsync() =>
        Task.FromResult<IReadOnlyList<PlayerRating>>([]);
    public Task<PlayerRating?> GetByEmailAsync(string email) =>
        Task.FromResult<PlayerRating?>(null);
}
