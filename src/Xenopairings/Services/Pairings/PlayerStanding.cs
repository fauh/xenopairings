namespace Xenopairings.Services.Pairings;

/// <summary>
/// Summarises a player's current tournament standing used as input to Swiss pairing.
/// </summary>
public record PlayerStanding(
    Guid PlayerId,
    int Wins,
    int TotalPoints
);
