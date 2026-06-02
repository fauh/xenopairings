using Microsoft.EntityFrameworkCore;
using Xenopairings.Data;
using Xenopairings.Models;
using Xenopairings.Services.Pairings;

namespace Xenopairings.Services.Standings;

/// <summary>
/// Computes individual player standings for a tournament from all scored matches.
///
/// GW scoring:
///   Win  = own raw score &gt; opponent raw score
///   Draw = equal raw scores
///   Loss = own raw score &lt; opponent raw score
///   TotalPoints = sum of raw scores
///
/// WTC scoring:
///   Raw scores are converted to 0–20 game points via <see cref="WtcScoring"/>.
///   Win  = own GP &gt; 10
///   Draw = own GP == 10  (always paired: if you get 10, opponent gets 10)
///   Loss = own GP &lt; 10
///   TotalPoints = sum of game points
///
/// Sort order: Wins desc, TotalPoints desc. Draws and dropped players are included.
/// </summary>
public sealed class StandingsService(AppDbContext db)
{
    public async Task<IReadOnlyList<PlayerStanding>> ComputeAsync(
        Guid tournamentId, ScoringSystem scoringSystem = ScoringSystem.Gw)
    {
        var players = await db.Players
            .Where(p => p.TournamentId == tournamentId)
            .ToListAsync();

        if (players.Count == 0)
            return [];

        var matches = await db.Matches
            .Include(m => m.Round)
            .Where(m => m.Round.TournamentId == tournamentId && m.IsScored)
            .ToListAsync();

        var wins   = players.ToDictionary(p => p.Id, _ => 0);
        var draws  = players.ToDictionary(p => p.Id, _ => 0);
        var losses = players.ToDictionary(p => p.Id, _ => 0);
        var points = players.ToDictionary(p => p.Id, _ => 0);

        foreach (var match in matches)
        {
            var p1 = match.Player1Id;
            var p2 = match.Player2Id;
            if (p1 is null) continue;

            if (p2 is null)
            {
                // Bye — Player1 always wins, 0 points awarded
                Increment(wins, p1.Value);
                continue;
            }

            var raw1 = match.Player1Score ?? 0;
            var raw2 = match.Player2Score ?? 0;

            int s1, s2;
            if (scoringSystem == ScoringSystem.Wtc)
            {
                (s1, s2) = WtcScoring.ConvertToGamePoints(raw1, raw2);
            }
            else
            {
                s1 = raw1;
                s2 = raw2;
            }

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
                Draws:       draws.GetValueOrDefault(p.Id),
                TotalPoints: points.GetValueOrDefault(p.Id)))
            .OrderByDescending(s => s.Wins)
            .ThenByDescending(s => s.TotalPoints)
            .ToList();
    }

    private static void Increment(Dictionary<Guid, int> d, Guid k) => d[k] = d.GetValueOrDefault(k) + 1;
    private static void Add(Dictionary<Guid, int> d, Guid k, int v) => d[k] = d.GetValueOrDefault(k) + v;
}
