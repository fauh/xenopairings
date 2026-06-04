using Microsoft.EntityFrameworkCore;
using Xenopairings.Data;
using Xenopairings.Models;

namespace Xenopairings.Services.Teams;

public sealed class TeamService(AppDbContext db, TokenGenerator tokenGenerator) : ITeamService
{
    public async Task<Team> CreateTeamAsync(Guid tournamentId, string name, Guid captainPlayerId)
    {
        var tournament = await db.Tournaments.FindAsync(tournamentId)
            ?? throw new InvalidOperationException("Tournament not found.");

        if (!tournament.IsTeamEvent)
            throw new InvalidOperationException("This tournament is not a team event.");

        var team = new Team
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            Name = name.Trim(),
            CaptainPlayerId = captainPlayerId,
            InviteToken = tokenGenerator.RandomUrlSafeString(12),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Teams.Add(team);

        // Assign captain to the team
        var captain = await db.Players.FindAsync(captainPlayerId);
        if (captain is not null)
            captain.TeamId = team.Id;

        await db.SaveChangesAsync();
        return team;
    }

    public Task<Team?> GetByInviteTokenAsync(string token) =>
        db.Teams
            .Include(t => t.Players)
            .FirstOrDefaultAsync(t => t.InviteToken == token);

    public async Task JoinTeamAsync(Guid teamId, Guid playerId)
    {
        var team = await db.Teams
            .Include(t => t.Players)
            .FirstOrDefaultAsync(t => t.Id == teamId)
            ?? throw new InvalidOperationException("Team not found.");

        var tournament = await db.Tournaments.FindAsync(team.TournamentId)
            ?? throw new InvalidOperationException("Tournament not found.");

        var teamSize = tournament.TeamSize ?? int.MaxValue;
        if (team.Players.Count(p => !p.IsDropped) >= teamSize)
            throw new InvalidOperationException($"This team is already full ({teamSize} players).");

        var player = await db.Players.FindAsync(playerId)
            ?? throw new InvalidOperationException("Player not found.");

        if (player.TeamId is not null)
            throw new InvalidOperationException("You are already on a team in this tournament.");

        player.TeamId = teamId;
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Team>> ListByTournamentAsync(Guid tournamentId)
    {
        var teams = await db.Teams
            .Include(t => t.Players)
            .Where(t => t.TournamentId == tournamentId)
            .ToListAsync();
        return [.. teams.OrderBy(t => t.CreatedAt)];
    }

    public async Task<Player> AddPlayerManuallyAsync(Guid teamId, string name, string? email)
    {
        var team = await db.Teams
            .Include(t => t.Players)
            .FirstOrDefaultAsync(t => t.Id == teamId)
            ?? throw new InvalidOperationException("Team not found.");

        var tournament = await db.Tournaments.FindAsync(team.TournamentId)
            ?? throw new InvalidOperationException("Tournament not found.");

        var teamSize = tournament.TeamSize ?? int.MaxValue;
        var activeCount = team.Players.Count(p => !p.IsDropped);
        if (activeCount >= teamSize)
            throw new InvalidOperationException($"Team is already full ({teamSize} players).");

        var normalisedEmail = string.IsNullOrWhiteSpace(email) ? null
            : email.Trim().ToLowerInvariant();

        // Check not already registered
        if (normalisedEmail is not null)
        {
            var alreadyRegistered = await db.Players.AnyAsync(p =>
                p.TournamentId == team.TournamentId &&
                p.Email == normalisedEmail);
            if (alreadyRegistered)
                throw new InvalidOperationException($"{normalisedEmail} is already registered for this tournament.");
        }

        var player = new Player
        {
            Id = Guid.NewGuid(),
            TournamentId = team.TournamentId,
            Name = name.Trim(),
            Email = normalisedEmail,
            TeamId = teamId,
            EditToken = tokenGenerator.RandomUrlSafeString(22),
            RegisteredAt = DateTimeOffset.UtcNow,
        };

        db.Players.Add(player);
        await db.SaveChangesAsync();
        return player;
    }

    public async Task RemoveFromTeamAsync(Guid playerId)
    {
        var player = await db.Players.FindAsync(playerId);
        if (player is null) return;
        player.TeamId = null;
        // Also drop them from the tournament so they don't take a slot
        player.IsDropped = true;
        await db.SaveChangesAsync();
    }

    public async Task TransferCaptainAsync(Guid teamId, Guid newCaptainPlayerId)
    {
        var team = await db.Teams
            .Include(t => t.Players)
            .FirstOrDefaultAsync(t => t.Id == teamId)
            ?? throw new InvalidOperationException("Team not found.");

        if (team.Players.All(p => p.Id != newCaptainPlayerId))
            throw new InvalidOperationException("Player is not a member of this team.");

        team.CaptainPlayerId = newCaptainPlayerId;
        await db.SaveChangesAsync();
    }

    public async Task UpdateTeamMemberAsync(Guid playerId, string? armyFaction, string? armyList)
    {
        var player = await db.Players.FindAsync(playerId);
        if (player is null) return;
        player.ArmyFaction = string.IsNullOrWhiteSpace(armyFaction) ? null : armyFaction.Trim();
        player.ArmyList = string.IsNullOrWhiteSpace(armyList) ? null : armyList.Trim();
        await db.SaveChangesAsync();
    }
}
