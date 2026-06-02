namespace Xenopairings.Models;

/// <summary>
/// Global ELO rating for a player, keyed by their email address.
/// Persists across all tournaments. Created on first scored match; updated thereafter.
///
/// Players without an email address are not rated.
/// </summary>
public class PlayerRating
{
    public Guid Id { get; set; }
    /// <summary>Normalised to lowercase. Unique index.</summary>
    public string Email { get; set; } = string.Empty;
    /// <summary>Most recent name used in a tournament. Updated each time the rating changes.</summary>
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>Current ELO rating. Starting value: 1000.</summary>
    public double Rating { get; set; } = 1000.0;
    public int GamesPlayed { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>
    /// When false the profile page shows "Profile hidden" and no match history is visible.
    /// The player still appears on the leaderboard with their rating.
    /// </summary>
    public bool IsProfilePublic { get; set; } = true;
}
