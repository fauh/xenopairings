using Microsoft.EntityFrameworkCore;
using Xenopairings.Data;
using Xenopairings.Services.Pairings;

namespace Xenopairings.Services.Standings;

/// <summary>
/// Computes current standings for a tournament from all scored matches.
///
/// Scoring rules:
/// - Regular match: higher score = Win, equal scores = Draw, lower score = Loss.
///   Battle points contributed to TotalPoints = own score in that match.
/// - Bye match (Player2Id = null, IsScored = true): Player1 gets a Win, 0 battle points.
///
/// Sort order: Wins desc, then TotalPoints desc. Players with no scored matches appear
/// at the bottom with all zeroes.
/// </summary>
public sealed class StandingsService(AppDbContext db)
{
    public async Task<IReadOnlyList<PlayerStanding>> ComputeAsync(Guid tournamentId)
    {
        // Load all players for the tournament (including dropped — they keep their results)
        var players = await db.Players
            .Where(p => p.TournamentId == tournamentId)
            .ToListAsync();

        if (players.Count == 0)
            return [];

        // Load all scored matches for this tournament (via rounds)
        var matches = await db.Matches
            .Include(m => m.Round)
            .Where(m => m.Round.TournamentId == tournamentId && m.IsScored)
            .ToListAsync();

        // Accumulate per-player stats
        var wins   = new Dictionary<Guid, int>();
        var draws  = new Dictionary<Guid, int>();
        var losses = new Dictionary<Guid, int>();
        var points = new Dictionary<Guid, int>();

        foreach (var p in players)
        {
            wins[p.Id]   = 0;
            draws[p.Id]  = 0;
            losses[p.Id] = 0;
            points[p.Id] = 0;
        }

        foreach (var match in matches)
        {
            var p1 = match.Player1Id;
            var p2 = match.Player2Id;

            if (p1 is null) continue;  // shouldn't happen, but guard

            if (p2 is null)
            {
                // Bye — Player1 wins, no battle points
                Increment(wins, p1.Value);
                continue;
            }

            var s1 = match.Player1Score ?? 0;
            var s2 = match.Player2Score ?? 0;

            if (s1 > s2)
            {
                Increment(wins,   p1.Value);
                Increment(losses, p2.Value);
            }
            else if (s2 > s1)
            {
                Increment(losses, p1.Value);
                Increment(wins,   p2.Value);
            }
            else
            {
                Increment(draws, p1.Value);
                Increment(draws, p2.Value);
            }

            Add(points, p1.Value, s1);
            Add(points, p2.Value, s2);
        }

        return players
            .Select(p => new PlayerStanding(
                PlayerId:    p.Id,
                Wins:        wins.GetValueOrDefault(p.Id),
                TotalPoints: points.GetValueOrDefault(p.Id)))
            .OrderByDescending(s => s.Wins)
            .ThenByDescending(s => s.TotalPoints)
            .ToList();
    }

    private static void Increment(Dictionary<Guid, int> dict, Guid key)
    {
        dict.TryGetValue(key, out var v);
        dict[key] = v + 1;
    }

    private static void Add(Dictionary<Guid, int> dict, Guid key, int value)
    {
        dict.TryGetValue(key, out var v);
        dict[key] = v + value;
    }
}
