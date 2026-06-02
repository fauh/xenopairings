using Xenopairings.Models;

namespace Xenopairings.Services.Rounds;

public interface IRoundService
{
    /// <summary>
    /// Creates the next round for the tournament, generates Swiss pairings, and
    /// inserts the Match rows. Bye matches are auto-scored immediately.
    /// Throws <see cref="InvalidOperationException"/> if the most recent round is
    /// not yet complete, or if the tournament has no active players.
    /// </summary>
    Task<Round> CreateWithPairingsAsync(Guid tournamentId, string? missionLayout);

    Task<Round?> GetAsync(Guid roundId);
    Task<IReadOnlyList<Round>> ListByTournamentAsync(Guid tournamentId);

    /// <summary>Returns the matches for a round with Player1 and Player2 loaded.</summary>
    Task<IReadOnlyList<Match>> GetMatchesAsync(Guid roundId);

    /// <summary>
    /// Enters both scores for a match and marks it IsScored = true.
    /// Scores must be non-negative integers.
    /// </summary>
    Task EnterScoresAsync(Guid matchId, int player1Score, int player2Score);

    /// <summary>
    /// Marks a round complete. All matches in the round must be scored first.
    /// </summary>
    Task CompleteRoundAsync(Guid roundId);
}
