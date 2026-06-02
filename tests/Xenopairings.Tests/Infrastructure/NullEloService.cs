using Xenopairings.Models;
using Xenopairings.Services.Elo;
#pragma warning disable CS8619

namespace Xenopairings.Tests.Infrastructure;

/// <summary>No-op ELO service for tests.</summary>
public sealed class NullEloService : IEloService
{
    public Task UpdateMatchRatingsAsync(Guid matchId) => Task.CompletedTask;
    public Task<IReadOnlyList<PlayerRating>> GetLeaderboardAsync() =>
        Task.FromResult<IReadOnlyList<PlayerRating>>([]);
    public Task<PlayerRating?> GetByEmailAsync(string email) =>
        Task.FromResult<PlayerRating?>(null);
    public Task<(PlayerRating Rating, IReadOnlyList<PlayerRatingHistory> History)?> GetProfileAsync(Guid ratingId) =>
        Task.FromResult<(PlayerRating, IReadOnlyList<PlayerRatingHistory>)?>(null);
    public Task SetProfileVisibilityAsync(Guid ratingId, bool isPublic) => Task.CompletedTask;
}
