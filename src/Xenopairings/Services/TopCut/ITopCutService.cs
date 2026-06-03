using Xenopairings.Models;

namespace Xenopairings.Services.TopCut;

public interface ITopCutService
{
    /// <summary>
    /// Seeds the top-cut bracket from the current Swiss standings.
    /// Creates all first-round matches. Subsequent rounds are created as matches complete.
    /// Throws if TopCutSize is null, the bracket already exists, or not enough players.
    /// </summary>
    Task GenerateBracketAsync(Guid tournamentId);

    /// <summary>Returns all top-cut matches for the tournament, ordered by BracketRound then MatchNumber.</summary>
    Task<IReadOnlyList<TopCutMatch>> GetBracketAsync(Guid tournamentId);

    /// <summary>
    /// Records scores for a top-cut match, determines the winner, and auto-creates the
    /// next-round match if both feeding matches are now complete.
    /// </summary>
    Task EnterTopCutResultAsync(Guid matchId, int player1Score, int player2Score);
}
