using Xenopairings.Models;

namespace Xenopairings.Services.Players;

public interface IPlayerService
{
    Task<Player> RegisterAsync(RegisterPlayerRequest request);
    Task<Player?> GetByEditTokenAsync(string editToken);
    Task<IReadOnlyList<Player>> ListByTournamentAsync(Guid tournamentId);
    Task DropAsync(Guid playerId);
    Task UpdateRegistrationAsync(Guid playerId, string? armyFaction, string? armyList);
    /// <summary>Returns all active registrations for an email, with Tournament loaded.</summary>
    Task<IReadOnlyList<Player>> ListWithTournamentByEmailAsync(string email);
}

public record RegisterPlayerRequest(
    Guid TournamentId,
    string Name,
    string? Email,
    string? ArmyFaction,
    string? ArmyList,
    Guid? OrganizationId = null
);
