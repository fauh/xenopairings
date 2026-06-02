namespace Xenopairings.Services.Pairings;

/// <summary>
/// Summarises a team's current tournament standing used as input to Swiss team pairing.
/// </summary>
public record TeamStanding(
    Guid TeamId,
    int Wins,
    int Draws,
    int TotalPoints
);
