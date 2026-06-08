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
    /// Records a 1–5 sportsmanship rating from a player for their match.
    /// The submitting player must be Player1 or Player2 of the match.
    /// </summary>
    Task SetSportsRatingAsync(Guid matchId, Guid submittingPlayerId, int rating);

    /// <summary>
    /// Records scores using the standard breakdown (Primary + Secondary + Battle Ready).
    /// Computes and stores the totals in Player1Score / Player2Score for use by standings and ELO.
    /// Also stores the individual breakdown fields.
    /// </summary>
    Task EnterScoreBreakdownAsync(
        Guid matchId,
        int p1Primary, int p1Secondary, bool p1BattleReady,
        int p2Primary, int p2Secondary, bool p2BattleReady,
        bool? player1IsAttacker = null,
        bool? player1WentFirst = null);

    /// <summary>
    /// Overwrites the scores (and optional metadata) on any scored match, including those in
    /// completed rounds. Updates PlayerRatingHistory raw scores but does NOT recalculate ELO
    /// (the original ELO delta is kept as-is).
    /// </summary>
    Task UpdateScoreAsync(
        Guid matchId,
        int player1Score,
        int player2Score,
        bool? player1IsAttacker = null,
        bool? player1WentFirst = null);

    /// <summary>
    /// Enters both scores (and optional role metadata) for a match, marks it IsScored = true.
    /// Scores must be non-negative integers.
    /// </summary>
    Task EnterScoresAsync(
        Guid matchId,
        int player1Score,
        int player2Score,
        bool? player1IsAttacker = null,
        bool? player1WentFirst = null);

    /// <summary>
    /// Lets a player submit the result of their own match.
    /// <paramref name="submittingPlayerId"/> must be Player1 or Player2 of the match.
    /// Scores are given from the submitting player's perspective:
    /// <paramref name="myScore"/> is their score, <paramref name="opponentScore"/> is the opponent's.
    /// </summary>
    Task SubmitMatchResultAsync(
        Guid matchId,
        Guid submittingPlayerId,
        int myScore,
        int opponentScore,
        bool iWentFirst,
        bool iWasAttacker);

    /// <summary>
    /// Marks a round complete. All matches in the round must be scored first.
    /// For team rounds, all individual matches within all team matchups must be scored.
    /// </summary>
    Task CompleteRoundAsync(Guid roundId);

    // ── Re-pairing ────────────────────────────────────────────────────────────

    /// <summary>
    /// Deletes all matches in the round and regenerates pairings randomly.
    /// Throws if any match in the round has already been scored.
    /// </summary>
    Task RepairRandomAsync(Guid roundId);

    /// <summary>
    /// Replaces all match rows in the round with the provided manual pairings.
    /// Each entry is (player1Id, player2Id?, tableNumber). Player2Id null = bye.
    /// Throws if any existing match in the round has been scored, or if a player
    /// appears more than once, or if table numbers are not unique.
    /// </summary>
    Task SetManualPairingsAsync(
        Guid roundId,
        IReadOnlyList<(Guid player1Id, Guid? player2Id, int tableNumber)> pairings);

    // ── Team-event additions ──────────────────────────────────────────────────

    /// <summary>Returns the team matchups for a round with Team1, Team2 loaded.</summary>
    Task<IReadOnlyList<TeamMatchup>> GetTeamMatchupsAsync(Guid roundId);

    /// <summary>
    /// Records one individual player matchup within a team matchup.
    /// Each player must belong to the correct team. Players cannot be matched twice in the same round.
    /// </summary>
    Task<Match> AddMatchToTeamMatchupAsync(Guid teamMatchupId, Guid player1Id, Guid player2Id);
}
