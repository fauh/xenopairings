namespace Xenopairings.Services.Elo;

/// <summary>
/// One row per tournament that has ELO history entries.
/// Used by the admin audit table so an admin can review and revert ELO impacts.
/// </summary>
public sealed class EloTournamentSummary
{
    public Guid TournamentId { get; init; }
    public string TournamentTitle { get; init; } = string.Empty;
    public string TournamentSlug { get; init; } = string.Empty;
    public DateTimeOffset PlayedAt { get; init; }   // latest entry in this tournament
    public int MatchCount { get; init; }             // number of history rows
    public int PlayerCount { get; init; }            // distinct players affected

    /// <summary>Per-player breakdown: who was affected and by how much.</summary>
    public IReadOnlyList<EloPlayerDelta> PlayerDeltas { get; init; } = [];
}

public sealed class EloPlayerDelta
{
    public string DisplayName { get; init; } = string.Empty;
    public double RatingBefore { get; init; }
    public double RatingAfter { get; init; }
    public double Delta => RatingAfter - RatingBefore;
    public int GamesInTournament { get; init; }
}
