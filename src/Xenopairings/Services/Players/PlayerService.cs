using Microsoft.EntityFrameworkCore;
using Xenopairings.Data;
using Xenopairings.Models;

namespace Xenopairings.Services.Players;

public class PlayerService(
    AppDbContext db,
    TokenGenerator tokenGenerator,
    ILogger<PlayerService> logger) : IPlayerService
{
    public async Task<Player> RegisterAsync(RegisterPlayerRequest request)
    {
        var player = new Player
        {
            Id = Guid.NewGuid(),
            TournamentId = request.TournamentId,
            Name = request.Name.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant(),
            ArmyFaction = string.IsNullOrWhiteSpace(request.ArmyFaction) ? null : request.ArmyFaction.Trim(),
            ArmyList = string.IsNullOrWhiteSpace(request.ArmyList) ? null : request.ArmyList.Trim(),
            EditToken = tokenGenerator.RandomUrlSafeString(22),
            RegisteredAt = DateTimeOffset.UtcNow,
            IsDropped = false,
        };

        db.Players.Add(player);
        await db.SaveChangesAsync();

        logger.LogInformation("Player {Name} registered for tournament {TournamentId}.", player.Name, player.TournamentId);
        return player;
    }

    public Task<Player?> GetByEditTokenAsync(string editToken) =>
        db.Players
            .Include(p => p.Tournament)
            .FirstOrDefaultAsync(p => p.EditToken == editToken);

    public async Task<IReadOnlyList<Player>> ListByTournamentAsync(Guid tournamentId)
    {
        var players = await db.Players
            .Where(p => p.TournamentId == tournamentId)
            .OrderBy(p => p.RegisteredAt.UtcTicks)
            .ToListAsync();
        return [.. players];
    }

    public async Task DropAsync(Guid playerId)
    {
        var player = await db.Players.FindAsync(playerId);
        if (player is null)
        {
            logger.LogWarning("DropAsync: player {PlayerId} not found.", playerId);
            return;
        }

        player.IsDropped = true;
        await db.SaveChangesAsync();
    }

    public async Task UpdateRegistrationAsync(Guid playerId, string? armyFaction, string? armyList)
    {
        var player = await db.Players.FindAsync(playerId);
        if (player is null)
        {
            logger.LogWarning("UpdateRegistrationAsync: player {PlayerId} not found.", playerId);
            return;
        }

        player.ArmyFaction = string.IsNullOrWhiteSpace(armyFaction) ? null : armyFaction.Trim();
        player.ArmyList = string.IsNullOrWhiteSpace(armyList) ? null : armyList.Trim();
        await db.SaveChangesAsync();
    }
}
