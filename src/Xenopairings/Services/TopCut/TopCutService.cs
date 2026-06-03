using Microsoft.EntityFrameworkCore;
using Xenopairings.Data;
using Xenopairings.Models;
using Xenopairings.Services.Standings;

namespace Xenopairings.Services.TopCut;

public sealed class TopCutService(
    AppDbContext db,
    StandingsService standingsService,
    ILogger<TopCutService> logger) : ITopCutService
{
    public async Task GenerateBracketAsync(Guid tournamentId)
    {
        var tournament = await db.Tournaments.FindAsync(tournamentId)
            ?? throw new InvalidOperationException("Tournament not found.");

        if (tournament.TopCutSize is null)
            throw new InvalidOperationException("This tournament has no top cut configured.");

        var cutSize = tournament.TopCutSize.Value;
        if (cutSize is not (4 or 8 or 16 or 32))
            throw new InvalidOperationException("Top cut size must be 4, 8, 16, or 32.");

        var existing = await db.TopCutMatches.AnyAsync(tc => tc.TournamentId == tournamentId);
        if (existing)
            throw new InvalidOperationException("Top cut bracket already exists for this tournament.");

        // Seed from Swiss standings
        var standings = await standingsService.ComputeAsync(
            tournamentId, tournament.ScoringSystem, tournament.Tiebreakers);

        if (standings.Count < cutSize)
            throw new InvalidOperationException(
                $"Not enough players ({standings.Count}) for a top-{cutSize} cut.");

        var players = await db.Players
            .Where(p => p.TournamentId == tournamentId && !p.IsDropped)
            .ToListAsync();

        var playerById = players.ToDictionary(p => p.Id);
        // seed → player: seed 1 = highest standing
        var seedToPlayerId = standings
            .Take(cutSize)
            .Select((s, i) => (seed: i + 1, playerId: s.PlayerId))
            .ToDictionary(x => x.seed, x => x.playerId);

        // Standard single-elimination seeding pairs for R1
        var r1Pairs = BuildR1Seeds(cutSize);

        foreach (var (seed1, seed2, matchNum) in r1Pairs)
        {
            var tc = new TopCutMatch
            {
                Id = Guid.NewGuid(),
                TournamentId = tournamentId,
                BracketRound = 1,
                MatchNumber = matchNum,
                Seed1 = seed1,
                Seed2 = seed2,
                Player1Id = seedToPlayerId.GetValueOrDefault(seed1),
                Player2Id = seed2 == 0 ? null : seedToPlayerId.GetValueOrDefault(seed2),
            };
            // Bye match: auto-score
            if (seed2 == 0)
            {
                tc.IsScored = true;
                tc.WinnerId = tc.Player1Id;
                tc.Player1Score = 0;
                tc.Player2Score = 0;
            }
            db.TopCutMatches.Add(tc);
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Generated top-{CutSize} bracket for tournament {TournamentId}.", cutSize, tournamentId);
    }

    public async Task<IReadOnlyList<TopCutMatch>> GetBracketAsync(Guid tournamentId)
    {
        var matches = await db.TopCutMatches
            .Include(tc => tc.Player1)
            .Include(tc => tc.Player2)
            .Where(tc => tc.TournamentId == tournamentId)
            .ToListAsync();
        return [.. matches.OrderBy(tc => tc.BracketRound).ThenBy(tc => tc.MatchNumber)];
    }

    public async Task EnterTopCutResultAsync(Guid matchId, int player1Score, int player2Score)
    {
        if (player1Score < 0 || player2Score < 0)
            throw new ArgumentException("Scores must be non-negative.");
        if (player1Score == player2Score)
            throw new InvalidOperationException("Top cut matches cannot end in a draw. Enter different scores.");

        var match = await db.TopCutMatches.FindAsync(matchId)
            ?? throw new InvalidOperationException("Top cut match not found.");

        match.Player1Score = player1Score;
        match.Player2Score = player2Score;
        match.IsScored = true;
        match.WinnerId = player1Score > player2Score ? match.Player1Id : match.Player2Id;

        // Determine the total rounds in this bracket
        var allMatches = await db.TopCutMatches
            .Where(tc => tc.TournamentId == match.TournamentId)
            .ToListAsync();

        var maxRound = allMatches.Max(tc => tc.BracketRound);
        var isFinal = match.BracketRound == maxRound && allMatches.Count(tc => tc.BracketRound == maxRound) == 1;

        if (!isFinal)
        {
            // Check if the sibling match (same round, adjacent match number) is also done
            // so we can create the next-round match
            var nextRound = match.BracketRound + 1;
            var siblingMatchNumber = (match.MatchNumber % 2 == 1)
                ? match.MatchNumber + 1
                : match.MatchNumber - 1;
            var sibling = allMatches.FirstOrDefault(
                tc => tc.BracketRound == match.BracketRound && tc.MatchNumber == siblingMatchNumber);

            if (sibling?.IsScored == true && sibling.WinnerId.HasValue && match.WinnerId.HasValue)
            {
                // Both feeders done — create next-round match
                var nextMatchNumber = (match.MatchNumber + 1) / 2;
                var nextExists = allMatches.Any(tc => tc.BracketRound == nextRound && tc.MatchNumber == nextMatchNumber);

                if (!nextExists)
                {
                    // Lower MatchNumber winner → Player1, higher → Player2
                    var lowerMatch = match.MatchNumber < siblingMatchNumber ? match : sibling;
                    var higherMatch = match.MatchNumber < siblingMatchNumber ? sibling : match;

                    db.TopCutMatches.Add(new TopCutMatch
                    {
                        Id = Guid.NewGuid(),
                        TournamentId = match.TournamentId,
                        BracketRound = nextRound,
                        MatchNumber = nextMatchNumber,
                        Seed1 = 0,
                        Seed2 = 0,
                        Player1Id = lowerMatch.WinnerId,
                        Player2Id = higherMatch.WinnerId,
                    });
                }
            }
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Top cut match {MatchId} scored. Winner: {WinnerId}.", matchId, match.WinnerId);
    }

    // ── Seeding helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns the (seed1, seed2, matchNumber) pairs for the first round of a single-elimination bracket.
    /// Standard seeding: 1v cutSize, 2v cutSize-1, etc., arranged so higher seeds can't meet until later rounds.
    /// </summary>
    private static List<(int seed1, int seed2, int matchNum)> BuildR1Seeds(int cutSize)
    {
        // Build standard bracket order: protect top seeds
        var seeds = BuildBracketOrder(cutSize);
        var pairs = new List<(int, int, int)>();
        for (var i = 0; i < seeds.Count; i += 2)
            pairs.Add((seeds[i], seeds[i + 1], i / 2 + 1));
        return pairs;
    }

    private static List<int> BuildBracketOrder(int n)
    {
        if (n == 1) return [1];
        var half = BuildBracketOrder(n / 2);
        var result = new List<int>();
        foreach (var s in half)
        {
            result.Add(s);
            result.Add(n + 1 - s);
        }
        return result;
    }
}
