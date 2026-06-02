using Microsoft.EntityFrameworkCore;
using Xenopairings.Data;
using Xenopairings.Models;
using Xenopairings.Services.Pairings;

namespace Xenopairings.Services.Standings;

/// <summary>
/// Computes team standings by aggregating individual match scores across each team matchup.
///
/// Team score for a matchup = sum of game/battle points scored by that team's players.
///
/// Win / draw / loss determination:
///   GW:  team with more total BPs wins; equal = draw (threshold 0)
///   WTC: points are converted to GPs first; team wins only when GP difference
///        is strictly greater than <c>teamSize - 1</c>. Otherwise it is a draw.
///
/// Sort: Wins desc, TotalPoints desc.
/// </summary>
public sealed class TeamStandingsService(AppDbContext db)
{
    public async Task<IReadOnlyList<TeamStanding>> ComputeAsync(
        Guid tournamentId,
        ScoringSystem scoringSystem,
        int teamSize)
    {
        var teams = await db.Teams
            .Where(t => t.TournamentId == tournamentId)
            .ToListAsync();

        if (teams.Count == 0) return [];

        // Load all team matchups for completed rounds only
        var matchups = await db.TeamMatchups
            .Include(tm => tm.Round)
            .Where(tm => tm.Round.TournamentId == tournamentId && tm.Round.IsComplete)
            .ToListAsync();

        if (matchups.Count == 0)
            return teams.Select(t => new TeamStanding(t.Id, 0, 0, 0)).ToList();

        var matchupIds = matchups.Select(tm => tm.Id).ToList();

        // Load all scored individual matches belonging to those matchups
        var individualMatches = await db.Matches
            .Where(m => m.TeamMatchupId != null
                     && matchupIds.Contains(m.TeamMatchupId!.Value)
                     && m.IsScored)
            .ToListAsync();

        var wins   = teams.ToDictionary(t => t.Id, _ => 0);
        var draws  = teams.ToDictionary(t => t.Id, _ => 0);
        var losses = teams.ToDictionary(t => t.Id, _ => 0);
        var points = teams.ToDictionary(t => t.Id, _ => 0);

        // Need to know which player belongs to which team for point aggregation
        var playerTeams = await db.Players
            .Where(p => p.TournamentId == tournamentId && p.TeamId != null)
            .Select(p => new { p.Id, p.TeamId })
            .ToListAsync();
        var playerToTeam = playerTeams.ToDictionary(p => p.Id, p => p.TeamId!.Value);

        foreach (var matchup in matchups)
        {
            if (matchup.Team2Id is null)
            {
                // Bye — Team1 gets a win, 0 points
                Increment(wins, matchup.Team1Id);
                continue;
            }

            // Sum up scores for each team across all individual matches in this matchup
            var matchesInMatchup = individualMatches
                .Where(m => m.TeamMatchupId == matchup.Id)
                .ToList();

            int team1Points = 0, team2Points = 0;

            foreach (var m in matchesInMatchup)
            {
                var raw1 = m.Player1Score ?? 0;
                var raw2 = m.Player2Score ?? 0;

                int s1, s2;
                if (scoringSystem == ScoringSystem.Wtc)
                    (s1, s2) = WtcScoring.ConvertToGamePoints(raw1, raw2);
                else
                    (s1, s2) = (raw1, raw2);

                // Attribute points to the correct team
                if (m.Player1Id.HasValue && playerToTeam.TryGetValue(m.Player1Id.Value, out var p1Team))
                {
                    if (p1Team == matchup.Team1Id) team1Points += s1;
                    else if (p1Team == matchup.Team2Id) team2Points += s1;
                }
                if (m.Player2Id.HasValue && playerToTeam.TryGetValue(m.Player2Id.Value, out var p2Team))
                {
                    if (p2Team == matchup.Team1Id) team1Points += s2;
                    else if (p2Team == matchup.Team2Id) team2Points += s2;
                }
            }

            Add(points, matchup.Team1Id, team1Points);
            Add(points, matchup.Team2Id.Value, team2Points);

            // Determine win/draw/loss
            var diff = Math.Abs(team1Points - team2Points);
            int drawThreshold = scoringSystem == ScoringSystem.Wtc ? teamSize - 1 : 0;

            if (diff <= drawThreshold)
            {
                Increment(draws, matchup.Team1Id);
                Increment(draws, matchup.Team2Id.Value);
            }
            else if (team1Points > team2Points)
            {
                Increment(wins,   matchup.Team1Id);
                Increment(losses, matchup.Team2Id.Value);
            }
            else
            {
                Increment(losses, matchup.Team1Id);
                Increment(wins,   matchup.Team2Id.Value);
            }
        }

        return teams
            .Select(t => new TeamStanding(
                TeamId:      t.Id,
                Wins:        wins.GetValueOrDefault(t.Id),
                Draws:       draws.GetValueOrDefault(t.Id),
                TotalPoints: points.GetValueOrDefault(t.Id)))
            .OrderByDescending(s => s.Wins)
            .ThenByDescending(s => s.TotalPoints)
            .ToList();
    }

    private static void Increment(Dictionary<Guid, int> d, Guid k) => d[k] = d.GetValueOrDefault(k) + 1;
    private static void Add(Dictionary<Guid, int> d, Guid k, int v) => d[k] = d.GetValueOrDefault(k) + v;
}
