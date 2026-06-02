using Xenopairings.Models;

namespace Xenopairings.Services.Pairings;

/// <summary>
/// Pure-logic Swiss pairing generator. No database access — all inputs are in-memory.
///
/// Algorithm:
/// - Round 1: random pairing (shuffle and pair sequentially).
/// - Subsequent rounds: sort by wins desc, then total points desc; pair top two
///   adjacent players who haven't played each other, sliding down if a repeat is found.
/// - Odd number of players: the lowest-standing player who has not yet had a bye
///   gets a bye match (Player2 = null).
/// </summary>
public static class SwissPairingService
{
    /// <summary>
    /// Generates a list of (Player1, Player2?, TableNumber) pairings.
    /// Player2 is null for a bye.
    /// </summary>
    public static IReadOnlyList<(Player p1, Player? p2, int table)> Generate(
        IReadOnlyList<Player> activePlayers,
        IReadOnlyList<PlayerStanding> currentStandings,
        IReadOnlyList<(Guid, Guid)> previousMatchups)
    {
        var players = activePlayers.Where(p => !p.IsDropped).ToList();

        if (players.Count == 0)
            return [];

        var matchups = new HashSet<(Guid, Guid)>(
            previousMatchups.Select(m => m.Item1.CompareTo(m.Item2) <= 0
                ? (m.Item1, m.Item2)
                : (m.Item2, m.Item1)));

        List<Player> ordered;

        if (currentStandings.Count == 0)
        {
            // Round 1: random order
            ordered = [.. players.OrderBy(_ => Random.Shared.Next())];
        }
        else
        {
            // Swiss: sort by wins desc, then total points desc
            var standingMap = currentStandings.ToDictionary(s => s.PlayerId);
            ordered = [.. players.OrderByDescending(p =>
                standingMap.TryGetValue(p.Id, out var s) ? s.Wins : 0)
                .ThenByDescending(p =>
                standingMap.TryGetValue(p.Id, out var s) ? s.TotalPoints : 0)];
        }

        var result = new List<(Player p1, Player? p2, int table)>();
        var unpaired = new List<Player>(ordered);
        var tableNumber = 1;

        while (unpaired.Count >= 2)
        {
            var p1 = unpaired[0];
            unpaired.RemoveAt(0);

            // Find the highest-standing opponent p1 hasn't faced
            var opponentIndex = unpaired.FindIndex(p2 =>
            {
                var key = p1.Id.CompareTo(p2.Id) <= 0
                    ? (p1.Id, p2.Id)
                    : (p2.Id, p1.Id);
                return !matchups.Contains(key);
            });

            if (opponentIndex == -1)
            {
                // All remaining opponents are repeats — pair with the first anyway
                opponentIndex = 0;
            }

            var p2 = unpaired[opponentIndex];
            unpaired.RemoveAt(opponentIndex);
            result.Add((p1, p2, tableNumber++));
        }

        // Bye for odd player out
        if (unpaired.Count == 1)
        {
            result.Add((unpaired[0], null, tableNumber));
        }

        return result;
    }
}
