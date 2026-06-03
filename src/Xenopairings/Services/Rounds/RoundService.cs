using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xenopairings.Data;
using Xenopairings.Models;
using Xenopairings.Services.Elo;
using Xenopairings.Services.Email;
using Xenopairings.Services.Pairings;
using Xenopairings.Services.Standings;

namespace Xenopairings.Services.Rounds;

public sealed class RoundService(
    AppDbContext db,
    StandingsService standingsService,
    TeamStandingsService teamStandingsService,
    IEmailSender emailSender,
    IOptions<EmailSettings> emailSettings,
    IEloService eloService,
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

        // Round 1: avoid pairing players from the same organisation where possible
        List<(Guid, Guid)>? sameOrgAvoid = null;
        if (roundNumber == 1)
        {
            sameOrgAvoid = activePlayers
                .Where(p => p.OrganizationId.HasValue)
                .GroupBy(p => p.OrganizationId!.Value)
                .SelectMany(g =>
                {
                    var ids = g.Select(p => p.Id).ToList();
                    return ids.SelectMany((a, i) => ids.Skip(i + 1).Select(b => (a, b)));
                })
                .ToList();
        }

        var pairings = SwissPairingService.Generate(
            activePlayers, standings,
            previousPairs.Select(m => (m.Player1Id!.Value, m.Player2Id!.Value)).ToList(),
            sameOrgAvoid);

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

        // Send round notification emails (fire-and-forget — don't fail round creation on email error)
        _ = Task.Run(() => SendIndividualRoundEmailsAsync(tournament, round, pairings));

        return round;
    }

    private async Task SendIndividualRoundEmailsAsync(
        Tournament tournament,
        Round round,
        IReadOnlyList<(Player p1, Player? p2, int table)> pairings)
    {
        var baseUrl = emailSettings.Value.BaseUrl.TrimEnd('/');
        var tournamentUrl = $"{baseUrl}/t/{tournament.Slug}";
        var missionText = string.IsNullOrWhiteSpace(round.MissionLayout)
            ? "" : $"\nMission: {round.MissionLayout}";

        foreach (var (p1, p2, table) in pairings)
        {
            if (!string.IsNullOrWhiteSpace(p1.Email))
            {
                var opponent = p2 is null ? "— (bye)" : p2.Name;
                await SendRoundEmailSafe(p1.Email, p1.Name, tournament.Title,
                    round.RoundNumber, table, opponent, missionText, tournamentUrl);
            }
            if (p2 is not null && !string.IsNullOrWhiteSpace(p2.Email))
            {
                await SendRoundEmailSafe(p2.Email, p2.Name, tournament.Title,
                    round.RoundNumber, table, p1.Name, missionText, tournamentUrl);
            }
        }
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

        _ = Task.Run(() => SendTeamRoundEmailsAsync(tournament, round, pairings, teamSize));

        return round;
    }

    private async Task SendTeamRoundEmailsAsync(
        Tournament tournament,
        Round round,
        IReadOnlyList<(Team t1, Team? t2, int tableGroupStart)> pairings,
        int teamSize)
    {
        var baseUrl = emailSettings.Value.BaseUrl.TrimEnd('/');
        var tournamentUrl = $"{baseUrl}/t/{tournament.Slug}";
        var missionText = string.IsNullOrWhiteSpace(round.MissionLayout)
            ? "" : $"\nMission: {round.MissionLayout}";

        foreach (var (t1, t2, tableGroupStart) in pairings)
        {
            var t1Name = t1.Name;
            var t2Name = t2?.Name ?? "— (bye)";
            var tables = teamSize > 1
                ? $"Tables {tableGroupStart}–{tableGroupStart + teamSize - 1}"
                : $"Table {tableGroupStart}";

            foreach (var player in t1.Players.Where(p => !p.IsDropped && !string.IsNullOrWhiteSpace(p.Email)))
            {
                await SendTeamRoundEmailSafe(player.Email!, player.Name, tournament.Title,
                    round.RoundNumber, t1Name, t2Name, tables, missionText, tournamentUrl);
            }
            if (t2 is not null)
            {
                foreach (var player in t2.Players.Where(p => !p.IsDropped && !string.IsNullOrWhiteSpace(p.Email)))
                {
                    await SendTeamRoundEmailSafe(player.Email!, player.Name, tournament.Title,
                        round.RoundNumber, t2.Name, t1Name, tables, missionText, tournamentUrl);
                }
            }
        }
    }

    // ── Re-pairing ────────────────────────────────────────────────────────────

    public async Task RepairRandomAsync(Guid roundId)
    {
        var round = await db.Rounds
            .Include(r => r.Tournament)
            .FirstOrDefaultAsync(r => r.Id == roundId)
            ?? throw new InvalidOperationException("Round not found.");

        var scored = await db.Matches.AnyAsync(m => m.RoundId == roundId && m.IsScored && m.Player2Id != null);
        if (scored)
            throw new InvalidOperationException("Cannot re-pair: scores have already been entered for this round.");

        // Delete existing matches
        var existing = await db.Matches.Where(m => m.RoundId == roundId).ToListAsync();
        db.Matches.RemoveRange(existing);

        // Also clear team matchups if any
        var existingTm = await db.TeamMatchups.Where(tm => tm.RoundId == roundId).ToListAsync();
        db.TeamMatchups.RemoveRange(existingTm);

        await db.SaveChangesAsync();

        // Regenerate using the standard flow (routing through tournament)
        var tournament = round.Tournament;
        var allRounds = await db.Rounds
            .Where(r => r.TournamentId == tournament.Id && r.Id != roundId)
            .OrderBy(r => r.RoundNumber).ToListAsync();

        if (tournament.IsTeamEvent)
        {
            await CreateTeamRoundAsync(tournament, round.RoundNumber, round.MissionLayout);
        }
        else
        {
            var activePlayers = await db.Players
                .Where(p => p.TournamentId == tournament.Id && !p.IsDropped)
                .ToListAsync();
            var standings = await standingsService.ComputeAsync(tournament.Id, tournament.ScoringSystem);
            var previousPairs = await db.Matches
                .Include(m => m.Round)
                .Where(m => m.Round.TournamentId == tournament.Id
                         && m.Round.Id != roundId
                         && m.Player1Id != null && m.Player2Id != null)
                .Select(m => new { m.Player1Id, m.Player2Id })
                .ToListAsync();

            List<(Guid, Guid)>? sameOrgAvoid = null;
            if (round.RoundNumber == 1)
            {
                sameOrgAvoid = activePlayers
                    .Where(p => p.OrganizationId.HasValue)
                    .GroupBy(p => p.OrganizationId!.Value)
                    .SelectMany(g => { var ids = g.Select(p => p.Id).ToList(); return ids.SelectMany((a, i) => ids.Skip(i + 1).Select(b => (a, b))); })
                    .ToList();
            }

            var pairings = SwissPairingService.Generate(
                activePlayers, standings,
                previousPairs.Select(m => (m.Player1Id!.Value, m.Player2Id!.Value)).ToList(),
                sameOrgAvoid);

            foreach (var (p1, p2, table) in pairings)
            {
                db.Matches.Add(new Match
                {
                    Id = Guid.NewGuid(),
                    RoundId = roundId,
                    TableNumber = table,
                    Player1Id = p1.Id,
                    Player2Id = p2?.Id,
                    IsScored = p2 is null,
                });
            }
            await db.SaveChangesAsync();
        }

        logger.LogInformation("Re-paired round {RoundId} randomly.", roundId);
    }

    public async Task SetManualPairingsAsync(
        Guid roundId,
        IReadOnlyList<(Guid player1Id, Guid? player2Id, int tableNumber)> pairings)
    {
        var round = await db.Rounds.FindAsync(roundId)
            ?? throw new InvalidOperationException("Round not found.");

        var scored = await db.Matches.AnyAsync(m => m.RoundId == roundId && m.IsScored && m.Player2Id != null);
        if (scored)
            throw new InvalidOperationException("Cannot change pairings: scores have already been entered.");

        // Validate: no player twice, no duplicate tables
        var allPlayerIds = pairings
            .SelectMany(p => new[] { (Guid?)p.player1Id, p.player2Id })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
        if (allPlayerIds.Count != allPlayerIds.Distinct().Count())
            throw new ArgumentException("A player appears in more than one match.");
        var tables = pairings.Select(p => p.tableNumber).ToList();
        if (tables.Count != tables.Distinct().Count())
            throw new ArgumentException("Duplicate table numbers.");

        // Replace matches
        var existing = await db.Matches.Where(m => m.RoundId == roundId).ToListAsync();
        db.Matches.RemoveRange(existing);

        foreach (var (p1, p2, table) in pairings)
        {
            db.Matches.Add(new Match
            {
                Id = Guid.NewGuid(),
                RoundId = roundId,
                TableNumber = table,
                Player1Id = p1,
                Player2Id = p2,
                IsScored = p2 is null,  // bye auto-scored
            });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Manual pairings set for round {RoundId} with {Count} matches.", roundId, pairings.Count);
    }

    // ── Email helpers ─────────────────────────────────────────────────────────

    private async Task SendRoundEmailSafe(
        string email, string playerName, string tournamentTitle,
        int roundNumber, int table, string opponentName,
        string missionText, string tournamentUrl)
    {
        try
        {
            var subject = $"Round {roundNumber} is ready — {tournamentTitle}";
            var body = $"""
                Hi {playerName},

                Round {roundNumber} of {tournamentTitle} has been paired!

                Your match:
                  Table:    {table}
                  Opponent: {opponentName}{missionText}

                View pairings and submit your result:
                {tournamentUrl}
                """;
            await emailSender.SendAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send round notification to {Email}.", email);
        }
    }

    private async Task SendTeamRoundEmailSafe(
        string email, string playerName, string tournamentTitle,
        int roundNumber, string yourTeam, string opponentTeam,
        string tables, string missionText, string tournamentUrl)
    {
        try
        {
            var subject = $"Round {roundNumber} is ready — {tournamentTitle}";
            var body = $"""
                Hi {playerName},

                Round {roundNumber} of {tournamentTitle} has been paired!

                Your team {yourTeam} faces {opponentTeam}
                  {tables}{missionText}

                View pairings at:
                {tournamentUrl}
                """;
            await emailSender.SendAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send team round notification to {Email}.", email);
        }
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

    public async Task UpdateScoreAsync(
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
            throw new InvalidOperationException("Cannot edit scores for a bye match.");

        match.Player1Score = player1Score;
        match.Player2Score = player2Score;
        if (player1IsAttacker.HasValue) match.Player1IsAttacker = player1IsAttacker;
        if (player1WentFirst.HasValue)  match.Player1WentFirst  = player1WentFirst;
        // Keep IsScored = true (was already scored)
        match.IsScored = true;

        // Update raw scores in PlayerRatingHistory so history reflects the correction.
        // ELO deltas (RatingBefore/RatingAfter) are NOT recalculated — the original
        // ELO change was already applied and retroactive rebalancing is out of scope.
        var histories = await db.PlayerRatingHistories
            .Where(h => h.TournamentId == match.Round.TournamentId)
            .ToListAsync();

        // We can't query by matchId directly since history doesn't store it.
        // Instead reload match with player navigation to match up by player+opponent emails.
        var matchWithPlayers = await db.Matches
            .Include(m => m.Player1)
            .Include(m => m.Player2)
            .Include(m => m.Round)
            .FirstOrDefaultAsync(m => m.Id == matchId);

        if (matchWithPlayers?.Player1?.Email is not null && matchWithPlayers.Player2?.Email is not null)
        {
            // Find the two history rows for this match (identified by both player emails and tournament)
            var p1Email = matchWithPlayers.Player1.Email.ToLowerInvariant();
            var p2Email = matchWithPlayers.Player2.Email.ToLowerInvariant();
            var tournamentId = matchWithPlayers.Round.TournamentId;

            var p1History = await db.PlayerRatingHistories
                .Where(h => h.TournamentId == tournamentId
                         && h.PlayerRating!.Email == p1Email
                         && h.OpponentEmail == p2Email)
                .FirstOrDefaultAsync();
            if (p1History is not null)
            {
                p1History.MyRawScore = player1Score;
                p1History.OpponentRawScore = player2Score;
            }

            var p2History = await db.PlayerRatingHistories
                .Where(h => h.TournamentId == tournamentId
                         && h.PlayerRating!.Email == p2Email
                         && h.OpponentEmail == p1Email)
                .FirstOrDefaultAsync();
            if (p2History is not null)
            {
                p2History.MyRawScore = player2Score;
                p2History.OpponentRawScore = player1Score;
            }
        }

        await db.SaveChangesAsync();
    }

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

        // ELO update — awaited directly. eloService is a scoped DbContext-backed service;
        // Task.Run would run after the scope is disposed and the DbContext torn down.
        try { await eloService.UpdateMatchRatingsAsync(match.Id); }
        catch (Exception ex) { logger.LogWarning(ex, "ELO update failed for match {MatchId}.", match.Id); }
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

        try { await eloService.UpdateMatchRatingsAsync(match.Id); }
        catch (Exception ex) { logger.LogWarning(ex, "ELO update failed for match {MatchId}.", match.Id); }
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
