using Xenopairings.Models;

namespace Xenopairings.Services.Pairings;

/// <summary>
/// Pure-logic Swiss pairing generator for team events. No database access.
///
/// Algorithm mirrors <see cref="SwissPairingService"/> but operates on <see cref="Team"/> entities.
/// Round 1: random order. Subsequent rounds: sort by wins desc, total points desc.
/// Odd number of teams: lowest-standing team that hasn't had a bye gets a bye matchup (Team2 = null).
/// </summary>
public static class SwissTeamPairingService
{
    /// <summary>
    /// Generates team pairings. Returns a list of (team1, team2?, tableGroupStart).
    /// team2 is null for a bye.
    /// </summary>
    public static IReadOnlyList<(Team t1, Team? t2, int tableGroupStart)> Generate(
        IReadOnlyList<Team> teams,
        IReadOnlyList<TeamStanding> currentStandings,
        IReadOnlyList<(Guid, Guid)> previousMatchups,
        int teamSize)
    {
        if (teams.Count == 0) return [];

        var matchups = new HashSet<(Guid, Guid)>(
            previousMatchups.Select(m => m.Item1.CompareTo(m.Item2) <= 0
                ? (m.Item1, m.Item2)
                : (m.Item2, m.Item1)));

        List<Team> ordered;

        if (currentStandings.Count == 0)
        {
            ordered = [.. teams.OrderBy(_ => Random.Shared.Next())];
        }
        else
        {
            var standingMap = currentStandings.ToDictionary(s => s.TeamId);
            ordered = [.. teams
                .OrderByDescending(t => standingMap.TryGetValue(t.Id, out var s) ? s.Wins : 0)
                .ThenByDescending(t => standingMap.TryGetValue(t.Id, out var s) ? s.TotalPoints : 0)];
        }

        var result = new List<(Team t1, Team? t2, int tableGroupStart)>();
        var unpaired = new List<Team>(ordered);
        var tableGroupStart = 1;

        while (unpaired.Count >= 2)
        {
            var t1 = unpaired[0];
            unpaired.RemoveAt(0);

            var opponentIndex = unpaired.FindIndex(t2 =>
            {
                var key = t1.Id.CompareTo(t2.Id) <= 0
                    ? (t1.Id, t2.Id)
                    : (t2.Id, t1.Id);
                return !matchups.Contains(key);
            });

            if (opponentIndex == -1) opponentIndex = 0;

            var t2 = unpaired[opponentIndex];
            unpaired.RemoveAt(opponentIndex);
            result.Add((t1, t2, tableGroupStart));
            tableGroupStart += teamSize;
        }

        if (unpaired.Count == 1)
            result.Add((unpaired[0], null, tableGroupStart));

        return result;
    }
}
