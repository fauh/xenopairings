using Xenopairings.Models;

namespace Xenopairings.Services.Elo;

public interface IEloService
{
    /// <summary>
    /// Updates the global ELO ratings for both players in a scored match.
    /// No-ops if: the match is a bye, either player has no email, or the match is not scored.
    /// </summary>
    Task UpdateMatchRatingsAsync(Guid matchId);

    /// <summary>Returns all ratings sorted by Rating descending.</summary>
    Task<IReadOnlyList<PlayerRating>> GetLeaderboardAsync();

    /// <summary>Returns the rating for a specific email, or null if they have never played.</summary>
    Task<PlayerRating?> GetByEmailAsync(string email);

    /// <summary>
    /// Returns the player's rating and full match history, ordered oldest-first.
    /// Returns null if the rating ID does not exist.
    /// </summary>
    Task<(PlayerRating Rating, IReadOnlyList<PlayerRatingHistory> History)?> GetProfileAsync(Guid ratingId);

    /// <summary>Toggles the IsProfilePublic flag. Caller must verify ownership.</summary>
    Task SetProfileVisibilityAsync(Guid ratingId, bool isPublic);
}
