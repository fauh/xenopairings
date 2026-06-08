using Xenopairings.Models;

namespace Xenopairings.Services.Elo;

public interface IEloService
{
    /// <summary>
    /// Processes ELO for all scored matches in a tournament using the snapshot approach:
    /// every match delta is calculated against the player's rating before the tournament started,
    /// then all deltas are summed and applied in one update.
    /// Creates PlayerRatingHistory entries for each match (RatingBefore = pre-tournament
    /// snapshot, RatingAfter = post-tournament rating).
    /// Should be called exactly once when a tournament is ended.
    /// </summary>
    Task ProcessTournamentAsync(Guid tournamentId);

    /// <summary>Returns all ratings sorted by Rating descending.</summary>
    Task<IReadOnlyList<PlayerRating>> GetLeaderboardAsync();

    /// <summary>Returns the rating for a specific email, or null if they have never played.</summary>
    Task<PlayerRating?> GetByEmailAsync(string email);

    /// <summary>
    /// Gets or creates a PlayerRating entry for the given email so the player appears
    /// on the leaderboard even before playing their first match.
    /// Called when a user verifies their email.
    /// </summary>
    Task EnsureRatingAsync(string email, string displayName);

    /// <summary>
    /// Updates the display name on an existing PlayerRating.
    /// No-op if no rating exists for this email.
    /// Called when a registered user registers for a tournament with a chosen name.
    /// </summary>
    Task UpdateDisplayNameAsync(string email, string displayName);

    /// <summary>
    /// Returns the player's rating and full match history, ordered oldest-first.
    /// Returns null if the rating ID does not exist.
    /// </summary>
    Task<(PlayerRating Rating, IReadOnlyList<PlayerRatingHistory> History)?> GetProfileAsync(Guid ratingId);

    /// <summary>Toggles the IsProfilePublic flag. Caller must verify ownership.</summary>
    Task SetProfileVisibilityAsync(Guid ratingId, bool isPublic);

    /// <summary>
    /// Returns ELO history grouped by tournament, newest-first, for the admin audit table.
    /// </summary>
    Task<IReadOnlyList<EloTournamentSummary>> GetHistoryByTournamentAsync();

    /// <summary>
    /// Reverts every ELO change from a tournament: subtracts (RatingAfter − RatingBefore)
    /// from each affected player's current rating, decrements GamesPlayed by their match
    /// count for that tournament, and deletes all history entries for it.
    /// </summary>
    Task RevertTournamentEloAsync(Guid tournamentId);
}
