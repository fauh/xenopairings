using Microsoft.EntityFrameworkCore;
using Xenopairings.Data;
using Xenopairings.Models;
using Xenopairings.Services.Pairings;
using Xenopairings.Services.Standings;

namespace Xenopairings.Services.Rounds;

public sealed class RoundService(
    AppDbContext db,
    StandingsService standingsService,
    TeamStandingsService teamStandingsService,
    ILogger<RoundService> logger) : IRoundService
{
    public async Task<Round> CreateWithPairingsAsync(Guid tournamentId, string? missionLayout)
    {
        var tournament = await db.Tournaments.FindAsync(tournamentId)
            ?? throw new InvalidOperationException("Tournament not found.");

        var existingRounds = await db.Rounds
            .Where(r => r.TournamentId == tournamentId)
            .OrderBy(r => r.RoundNumber)
            .ToListAsync();

        if (existingRounds.Any(r => !r.IsComplete))
            throw new InvalidOperationException(
                "Cannot create a new round while an incomplete round exists.");

        var nextRoundNumber = existingRounds.Count + 1;
        var layout = string.IsNullOrWhiteSpace(missionLayout) ? null : missionLayout.Trim();

        return tournament.IsTeamEvent
            ? await CreateTeamRoundAsync(tournament, nextRoundNumber, layout)
            : await CreateIndividualRoundAsync(tournament, nextRoundNumber, layout);
    }

    // ── Individual round ──────────────────────────────────────────────────────

    private async Task<Round> CreateIndividualRoundAsync(
        Tournament tournament, int roundNumber, string? missionLayout)
    {
        var activePlayers = await db.Players
            .Where(p => p.TournamentId == tournament.Id && !p.IsDropped)
            .ToListAsync();

        if (activePlayers.Count == 0)
            throw new InvalidOperationException("Cannot create a round with no active players.");

        var standings = await standingsService.ComputeAsync(tournament.Id, tournament.ScoringSystem);

        var previousPairs = await db.Matches
            .Include(m => m.Round)
            .Where(m => m.Round.TournamentId == tournament.Id
                     && m.Player1Id != null
                     && m.Player2Id != null)
            .Select(m => new { m.Player1Id, m.Player2Id })
            .ToListAsync();

        var pairings = SwissPairingService.Generate(
            activePlayers, standings,
            previousPairs.Select(m => (m.Player1Id!.Value, m.Player2Id!.Value)).ToList());

        var round = CreateRoundEntity(tournament.Id, roundNumber, missionLayout);
        db.Rounds.Add(round);

        foreach (var (p1, p2, table) in pairings)
        {
            db.Matches.Add(new Match
            {
                Id = Guid.NewGuid(),
                RoundId = round.Id,
                TableNumber = table,
                Player1Id = p1.Id,
                Player2Id = p2?.Id,
                IsScored = p2 is null,  // bye auto-scored
            });
        }

        await db.SaveChangesAsync();
        logger.LogInformation(
            "Created individual round {RoundNumber} for tournament {TournamentId}.",
            roundNumber, tournament.Id);
        return round;
    }

    // ── Team round ────────────────────────────────────────────────────────────

    private async Task<Round> CreateTeamRoundAsync(
        Tournament tournament, int roundNumber, string? missionLayout)
    {
        var teamSize = tournament.TeamSize
            ?? throw new InvalidOperationException("TeamSize is not set on this team event.");

        var teams = await db.Teams
            .Include(t => t.Players)
            .Where(t => t.TournamentId == tournament.Id)
            .ToListAsync();

        if (teams.Count == 0)
            throw new InvalidOperationException("Cannot create a round — no teams registered.");

        var incompleteTeams = teams.Where(t => t.Players.Count(p => !p.IsDropped) < teamSize).ToList();
        if (incompleteTeams.Count > 0)
            throw new InvalidOperationException(
                $"Cannot create a round — {incompleteTeams.Count} team(s) are not yet full " +
                $"(need {teamSize} players each): " +
                string.Join(", ", incompleteTeams.Select(t => t.Name)));

        var teamStandings = await teamStandingsService.ComputeAsync(
            tournament.Id, tournament.ScoringSystem, teamSize);

        var previousTeamMatchups = await db.TeamMatchups
            .Include(tm => tm.Round)
            .Where(tm => tm.Round.TournamentId == tournament.Id
                      && tm.Team1Id != null
                      && tm.Team2Id != null)
            .Select(tm => new { tm.Team1Id, tm.Team2Id })
            .ToListAsync();

        var pairings = SwissTeamPairingService.Generate(
            teams,
            teamStandings,
            previousTeamMatchups.Select(m => (m.Team1Id, m.Team2Id!.Value)).ToList(),
            teamSize);

        var round = CreateRoundEntity(tournament.Id, roundNumber, missionLayout);
        db.Rounds.Add(round);

        foreach (var (t1, t2, tableGroupStart) in pairings)
        {
            var matchup = new TeamMatchup
            {
                Id = Guid.NewGuid(),
                RoundId = round.Id,
                Team1Id = t1.Id,
                Team2Id = t2?.Id,
                TableGroupStart = tableGroupStart,
            };
            db.TeamMatchups.Add(matchup);

            // Bye team matchup — no individual matches to create (organizer doesn't need to record them)
            // The bye is reflected in team standings when the round is completed.
        }

        await db.SaveChangesAsync();
        logger.LogInformation(
            "Created team round {RoundNumber} for tournament {TournamentId} with {MatchupCount} team matchups.",
            roundNumber, tournament.Id, pairings.Count);
        return round;
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private static Round CreateRoundEntity(Guid tournamentId, int roundNumber, string? missionLayout) =>
        new()
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            RoundNumber = roundNumber,
            MissionLayout = missionLayout,
            IsComplete = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    // ── Queries ───────────────────────────────────────────────────────────────

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

    public async Task<IReadOnlyList<TeamMatchup>> GetTeamMatchupsAsync(Guid roundId)
    {
        var matchups = await db.TeamMatchups
            .Include(tm => tm.Team1).ThenInclude(t => t.Players)
            .Include(tm => tm.Team2!).ThenInclude(t => t.Players)
            .Where(tm => tm.RoundId == roundId)
            .OrderBy(tm => tm.TableGroupStart)
            .ToListAsync();
        return matchups;
    }

    public async Task<Match> AddMatchToTeamMatchupAsync(
        Guid teamMatchupId, Guid player1Id, Guid player2Id)
    {
        var matchup = await db.TeamMatchups
            .Include(tm => tm.Team1).ThenInclude(t => t.Players)
            .Include(tm => tm.Team2!).ThenInclude(t => t.Players)
            .FirstOrDefaultAsync(tm => tm.Id == teamMatchupId)
            ?? throw new InvalidOperationException("Team matchup not found.");

        // Verify players belong to the correct teams
        var team1PlayerIds = matchup.Team1.Players.Select(p => p.Id).ToHashSet();
        var team2PlayerIds = matchup.Team2?.Players.Select(p => p.Id).ToHashSet() ?? [];

        if (!team1PlayerIds.Contains(player1Id))
            throw new ArgumentException($"Player1 does not belong to {matchup.Team1.Name}.");
        if (!team2PlayerIds.Contains(player2Id))
            throw new ArgumentException($"Player2 does not belong to {matchup.Team2?.Name}.");

        // Check neither player is already matched in this round
        var roundId = matchup.RoundId;
        var existingMatchIds = await db.TeamMatchups
            .Where(tm => tm.RoundId == roundId)
            .Select(tm => tm.Id)
            .ToListAsync();

        var alreadyPaired = await db.Matches
            .Where(m => m.TeamMatchupId != null
                     && existingMatchIds.Contains(m.TeamMatchupId!.Value)
                     && (m.Player1Id == player1Id || m.Player1Id == player2Id
                      || m.Player2Id == player1Id || m.Player2Id == player2Id))
            .AnyAsync();

        if (alreadyPaired)
            throw new InvalidOperationException("One or both players are already matched in this round.");

        // Find the next available table number within this matchup's group
        var existingMatchCount = await db.Matches
            .CountAsync(m => m.TeamMatchupId == teamMatchupId);

        var match = new Match
        {
            Id = Guid.NewGuid(),
            RoundId = roundId,
            TableNumber = matchup.TableGroupStart + existingMatchCount,
            Player1Id = player1Id,
            Player2Id = player2Id,
            TeamMatchupId = teamMatchupId,
            IsScored = false,
        };

        db.Matches.Add(match);
        await db.SaveChangesAsync();
        return match;
    }

    // ── Score entry ───────────────────────────────────────────────────────────

    public async Task EnterScoresAsync(
        Guid matchId,
        int player1Score,
        int player2Score,
        bool? player1IsAttacker = null,
        bool? player1WentFirst = null)
    {
        if (player1Score < 0 || player2Score < 0)
            throw new ArgumentException("Scores must be non-negative.");

        var match = await db.Matches.FindAsync(matchId)
            ?? throw new InvalidOperationException($"Match {matchId} not found.");

        if (match.Player2Id is null)
            throw new InvalidOperationException("Cannot enter scores for a bye match.");

        match.Player1Score = player1Score;
        match.Player2Score = player2Score;
        if (player1IsAttacker.HasValue) match.Player1IsAttacker = player1IsAttacker;
        if (player1WentFirst.HasValue)  match.Player1WentFirst  = player1WentFirst;
        match.IsScored = true;
        await db.SaveChangesAsync();
    }

    public async Task SubmitMatchResultAsync(
        Guid matchId,
        Guid submittingPlayerId,
        int myScore,
        int opponentScore,
        bool iWentFirst,
        bool iWasAttacker)
    {
        if (myScore < 0 || opponentScore < 0)
            throw new ArgumentException("Scores must be non-negative.");

        var match = await db.Matches.FindAsync(matchId)
            ?? throw new InvalidOperationException($"Match {matchId} not found.");

        if (match.Player2Id is null)
            throw new InvalidOperationException("Cannot submit a result for a bye match.");

        bool isPlayer1 = match.Player1Id == submittingPlayerId;
        bool isPlayer2 = match.Player2Id == submittingPlayerId;

        if (!isPlayer1 && !isPlayer2)
            throw new InvalidOperationException("You are not a participant in this match.");

        if (isPlayer1)
        {
            match.Player1Score      = myScore;
            match.Player2Score      = opponentScore;
            match.Player1IsAttacker = iWasAttacker;
            match.Player1WentFirst  = iWentFirst;
        }
        else // isPlayer2
        {
            match.Player1Score      = opponentScore;
            match.Player2Score      = myScore;
            match.Player1IsAttacker = !iWasAttacker;
            match.Player1WentFirst  = !iWentFirst;
        }

        match.IsScored = true;
        await db.SaveChangesAsync();
    }

    public async Task CompleteRoundAsync(Guid roundId)
    {
        var round = await db.Rounds.FindAsync(roundId)
            ?? throw new InvalidOperationException($"Round {roundId} not found.");

        // For team rounds, check all individual matches in all team matchups
        var isTeamRound = await db.TeamMatchups.AnyAsync(tm => tm.RoundId == roundId);

        if (isTeamRound)
        {
            var matchupIds = await db.TeamMatchups
                .Where(tm => tm.RoundId == roundId && tm.Team2Id != null)
                .Select(tm => tm.Id)
                .ToListAsync();

            foreach (var matchupId in matchupIds)
            {
                var tournament = await db.Rounds
                    .Where(r => r.Id == roundId)
                    .Select(r => r.Tournament)
                    .FirstOrDefaultAsync();
                var teamSize = tournament?.TeamSize ?? 0;
                var matchCount = await db.Matches
                    .CountAsync(m => m.TeamMatchupId == matchupId);
                var scoredCount = await db.Matches
                    .CountAsync(m => m.TeamMatchupId == matchupId && m.IsScored);

                if (matchCount < teamSize)
                    throw new InvalidOperationException(
                        $"A team matchup only has {matchCount}/{teamSize} individual matches recorded.");
                if (scoredCount < matchCount)
                    throw new InvalidOperationException(
                        $"A team matchup has {matchCount - scoredCount} unscored match(es).");
            }
        }
        else
        {
            var unscored = await db.Matches
                .CountAsync(m => m.RoundId == roundId && !m.IsScored);
            if (unscored > 0)
                throw new InvalidOperationException(
                    $"Cannot complete round — {unscored} match(es) still need scores.");
        }

        round.IsComplete = true;
        await db.SaveChangesAsync();
        logger.LogInformation("Round {RoundId} marked complete.", roundId);
    }
}
