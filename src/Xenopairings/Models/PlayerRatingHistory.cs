namespace Xenopairings.Models;

/// <summary>
/// Records one ELO change event for a player.
/// One row is created for each participant in a scored match that has an email.
/// </summary>
public class PlayerRatingHistory
{
    public Guid Id { get; set; }
    public Guid PlayerRatingId { get; set; }
    public PlayerRating PlayerRating { get; set; } = null!;

    // Tournament context
    public Guid TournamentId { get; set; }
    public string TournamentTitle { get; set; } = string.Empty;
    public string TournamentSlug { get; set; } = string.Empty;

    // Opponent
    public string? OpponentName { get; set; }
    public string? OpponentEmail { get; set; }

    // Scores
    public int MyRawScore { get; set; }
    public int OpponentRawScore { get; set; }
    /// <summary>Fractional outcome used for ELO: 1.0=win, 0.5=draw, 0.0=loss (or GP/20 for WTC).</summary>
    public double ActualOutcome { get; set; }

    // Rating change
    public double RatingBefore { get; set; }
    public double RatingAfter { get; set; }

    public DateTimeOffset PlayedAt { get; set; }
}
