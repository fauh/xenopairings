using Microsoft.EntityFrameworkCore;
using Xenopairings.Data;
using Xenopairings.Models;
using Xenopairings.Services.Pairings;
using Xenopairings.Services.Standings;

namespace Xenopairings.Services.Rounds;

public sealed class RoundService(
    AppDbContext db,
    StandingsService standingsService,
    ILogger<RoundService> logger) : IRoundService
{
    public async Task<Round> CreateWithPairingsAsync(Guid tournamentId, string? missionLayout)
    {
        // Guard: previous round must be complete
        var existingRounds = await db.Rounds
            .Where(r => r.TournamentId == tournamentId)
            .OrderBy(r => r.RoundNumber)
            .ToListAsync();

        if (existingRounds.Any(r => !r.IsComplete))
            throw new InvalidOperationException(
                "Cannot create a new round while an incomplete round exists.");

        var nextRoundNumber = existingRounds.Count + 1;

        // Gather active players
        var activePlayers = await db.Players
            .Where(p => p.TournamentId == tournamentId && !p.IsDropped)
            .ToListAsync();

        if (activePlayers.Count == 0)
            throw new InvalidOperationException(
                "Cannot create a round with no active players.");

        // Compute current standings (empty list for round 1 → random pairing)
        var standings = await standingsService.ComputeAsync(tournamentId);

        // Gather all previous matchup pairs to avoid repeats
        var previousMatchups = await db.Matches
            .Include(m => m.Round)
            .Where(m => m.Round.TournamentId == tournamentId
                     && m.Player1Id != null
                     && m.Player2Id != null)
            .Select(m => new { m.Player1Id, m.Player2Id })
            .ToListAsync();

        var previousPairs = previousMatchups
            .Select(m => (m.Player1Id!.Value, m.Player2Id!.Value))
            .ToList();

        // Generate pairings
        var pairings = SwissPairingService.Generate(activePlayers, standings, previousPairs);

        // Persist round + matches in one transaction
        var round = new Round
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            RoundNumber = nextRoundNumber,
            MissionLayout = string.IsNullOrWhiteSpace(missionLayout) ? null : missionLayout.Trim(),
            IsComplete = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Rounds.Add(round);

        for (var i = 0; i < pairings.Count; i++)
        {
            var (p1, p2, table) = pairings[i];
            var isBye = p2 is null;

            var match = new Match
            {
                Id = Guid.NewGuid(),
                RoundId = round.Id,
                TableNumber = table,
                Player1Id = p1.Id,
                Player2Id = p2?.Id,
                // Bye matches are auto-scored: player gets a win, no battle points
                Player1Score = isBye ? null : null,
                Player2Score = null,
                IsScored = isBye,
            };

            db.Matches.Add(match);
        }

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Created round {RoundNumber} for tournament {TournamentId} with {MatchCount} matches.",
            nextRoundNumber, tournamentId, pairings.Count);

        return round;
    }

    public Task<Round?> GetAsync(Guid roundId) =>
        db.Rounds.FindAsync(roundId).AsTask();

    public async Task<IReadOnlyList<Round>> ListByTournamentAsync(Guid tournamentId)
    {
        var rounds = await db.Rounds
            .Where(r => r.TournamentId == tournamentId)
            .OrderBy(r => r.RoundNumber)
            .ToListAsync();
        return rounds;
    }

    public async Task<IReadOnlyList<Match>> GetMatchesAsync(Guid roundId)
    {
        var matches = await db.Matches
            .Include(m => m.Player1)
            .Include(m => m.Player2)
            .Where(m => m.RoundId == roundId)
            .OrderBy(m => m.TableNumber)
            .ToListAsync();
        return matches;
    }

    public async Task EnterScoresAsync(Guid matchId, int player1Score, int player2Score)
    {
        if (player1Score < 0 || player2Score < 0)
            throw new ArgumentException("Scores must be non-negative.");

        var match = await db.Matches.FindAsync(matchId)
            ?? throw new InvalidOperationException($"Match {matchId} not found.");

        if (match.Player2Id is null)
            throw new InvalidOperationException("Cannot enter scores for a bye match.");

        match.Player1Score = player1Score;
        match.Player2Score = player2Score;
        match.IsScored = true;
        await db.SaveChangesAsync();
    }

    public async Task CompleteRoundAsync(Guid roundId)
    {
        var round = await db.Rounds.FindAsync(roundId)
            ?? throw new InvalidOperationException($"Round {roundId} not found.");

        var unscored = await db.Matches
            .CountAsync(m => m.RoundId == roundId && !m.IsScored);

        if (unscored > 0)
            throw new InvalidOperationException(
                $"Cannot complete round — {unscored} match(es) still need scores.");

        round.IsComplete = true;
        await db.SaveChangesAsync();

        logger.LogInformation("Round {RoundId} marked complete.", roundId);
    }
}
