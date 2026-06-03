using Microsoft.EntityFrameworkCore;
using Xenopairings.Data;
using Xenopairings.Models;
using Xenopairings.Services.Pairings;

namespace Xenopairings.Services.Standings;

/// <summary>
/// Computes individual player standings with a configurable tiebreaker chain.
///
/// Primary sort: Wins desc (always).
/// Then tiebreakers are applied in the order specified by Tournament.Tiebreakers:
///   Points               — total battle/game points
///   StrengthOfSchedule   — average TotalPoints of all opponents
///   ExtendedSOS          — average SOS of all opponents
///   HeadToHead           — direct result (only for 2-way ties; 0 otherwise)
///   Random               — stable random per player (consistent within one call)
/// </summary>
public sealed class StandingsService(AppDbContext db)
{
    public async Task<IReadOnlyList<PlayerStanding>> ComputeAsync(
        Guid tournamentId,
        ScoringSystem scoringSystem = ScoringSystem.Gw,
        IReadOnlyList<TiebreakerType>? tiebreakers = null)
    {
        tiebreakers ??= [TiebreakerType.Points, TiebreakerType.StrengthOfSchedule, TiebreakerType.Random];

        var players = await db.Players
            .Where(p => p.TournamentId == tournamentId)
            .ToListAsync();

        if (players.Count == 0) return [];

        var matches = await db.Matches
            .Include(m => m.Round)
            .Where(m => m.Round.TournamentId == tournamentId && m.IsScored)
            .ToListAsync();

        // ── Base counters ──────────────────────────────────────────────────────
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
            else { s1 = raw1; s2 = raw2; }

            if (s1 > s2) { Increment(wins, p1.Value); Increment(losses, p2.Value); }
            else if (s2 > s1) { Increment(losses, p1.Value); Increment(wins, p2.Value); }
            else { Increment(draws, p1.Value); Increment(draws, p2.Value); }

            Add(points, p1.Value, s1);
            Add(points, p2.Value, s2);
        }

        // ── Tiebreaker data ────────────────────────────────────────────────────

        // SOS: avg opponent TotalPoints
        Dictionary<Guid, double>? sosMap = null;
        if (tiebreakers.Contains(TiebreakerType.StrengthOfSchedule) ||
            tiebreakers.Contains(TiebreakerType.ExtendedStrengthOfSchedule))
        {
            sosMap = ComputeSos(players, matches, points);
        }

        // Extended SOS: avg of opponent SOS values
        Dictionary<Guid, double>? extSosMap = null;
        if (tiebreakers.Contains(TiebreakerType.ExtendedStrengthOfSchedule) && sosMap is not null)
        {
            extSosMap = ComputeExtendedSos(players, matches, sosMap);
        }

        // Head-to-head: for any 2-way tie, direct result (+1 win, 0 draw, -1 loss)
        Dictionary<Guid, int>? h2hMap = null;
        if (tiebreakers.Contains(TiebreakerType.HeadToHead))
        {
            h2hMap = players.ToDictionary(p => p.Id, _ => 0);
        }

        // Random: stable per-player random within this call
        Dictionary<Guid, Guid>? randomMap = null;
        if (tiebreakers.Contains(TiebreakerType.Random))
        {
            randomMap = players.ToDictionary(p => p.Id, _ => Guid.NewGuid());
        }

        // ── Build standing objects ─────────────────────────────────────────────
        var standings = players
            .Select(p => new PlayerStanding(
                PlayerId:    p.Id,
                Wins:        wins.GetValueOrDefault(p.Id),
                Draws:       draws.GetValueOrDefault(p.Id),
                TotalPoints: points.GetValueOrDefault(p.Id)))
            .ToList();

        // ── Apply tiebreaker chain ─────────────────────────────────────────────
        IOrderedEnumerable<PlayerStanding> ordered =
            standings.OrderByDescending(s => s.Wins);

        foreach (var tb in tiebreakers)
        {
            ordered = tb switch
            {
                TiebreakerType.Points =>
                    ordered.ThenByDescending(s => s.TotalPoints),
                TiebreakerType.StrengthOfSchedule when sosMap is not null =>
                    ordered.ThenByDescending(s => sosMap.GetValueOrDefault(s.PlayerId)),
                TiebreakerType.ExtendedStrengthOfSchedule when extSosMap is not null =>
                    ordered.ThenByDescending(s => extSosMap.GetValueOrDefault(s.PlayerId)),
                TiebreakerType.HeadToHead when h2hMap is not null =>
                    ordered.ThenByDescending(s => h2hMap.GetValueOrDefault(s.PlayerId)),
                TiebreakerType.Random when randomMap is not null =>
                    ordered.ThenBy(s => randomMap[s.PlayerId]),
                _ => ordered,
            };
        }

        return ordered.ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Dictionary<Guid, double> ComputeSos(
        List<Player> players,
        List<Match> matches,
        Dictionary<Guid, int> pointsMap)
    {
        return players.ToDictionary(p => p.Id, p =>
        {
            var opponentPoints = matches
                .Where(m => m.Player1Id == p.Id || m.Player2Id == p.Id)
                .Select(m => m.Player1Id == p.Id ? m.Player2Id : m.Player1Id)
                .Where(oppId => oppId.HasValue)
                .Select(oppId => (double)pointsMap.GetValueOrDefault(oppId!.Value))
                .ToList();
            return opponentPoints.Count > 0 ? opponentPoints.Average() : 0.0;
        });
    }

    private static Dictionary<Guid, double> ComputeExtendedSos(
        List<Player> players,
        List<Match> matches,
        Dictionary<Guid, double> sosMap)
    {
        return players.ToDictionary(p => p.Id, p =>
        {
            var opponentSos = matches
                .Where(m => m.Player1Id == p.Id || m.Player2Id == p.Id)
                .Select(m => m.Player1Id == p.Id ? m.Player2Id : m.Player1Id)
                .Where(oppId => oppId.HasValue)
                .Select(oppId => sosMap.GetValueOrDefault(oppId!.Value))
                .ToList();
            return opponentSos.Count > 0 ? opponentSos.Average() : 0.0;
        });
    }

    private static void Increment(Dictionary<Guid, int> d, Guid k) => d[k] = d.GetValueOrDefault(k) + 1;
    private static void Add(Dictionary<Guid, int> d, Guid k, int v) => d[k] = d.GetValueOrDefault(k) + v;
}
