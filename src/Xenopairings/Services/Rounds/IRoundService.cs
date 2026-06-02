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
    /// For team rounds, all individual matches within all team matchups must be scored.
    /// </summary>
    Task CompleteRoundAsync(Guid roundId);

    // ── Team-event additions ──────────────────────────────────────────────────

    /// <summary>Returns the team matchups for a round with Team1, Team2 loaded.</summary>
    Task<IReadOnlyList<TeamMatchup>> GetTeamMatchupsAsync(Guid roundId);

    /// <summary>
    /// Records one individual player matchup within a team matchup.
    /// Each player must belong to the correct team. Players cannot be matched twice in the same round.
    /// </summary>
    Task<Match> AddMatchToTeamMatchupAsync(Guid teamMatchupId, Guid player1Id, Guid player2Id);
}
