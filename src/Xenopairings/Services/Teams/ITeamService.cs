using Xenopairings.Models;

namespace Xenopairings.Services.Teams;

public interface ITeamService
{
    Task<Team> CreateTeamAsync(Guid tournamentId, string name, Guid captainPlayerId);
    Task<Team?> GetByInviteTokenAsync(string token);
    /// <summary>Adds a player to the team. Throws if full or already on a team.</summary>
    Task JoinTeamAsync(Guid teamId, Guid playerId);
    /// <summary>Returns all teams for the tournament with Players navigations loaded.</summary>
    Task<IReadOnlyList<Team>> ListByTournamentAsync(Guid tournamentId);

    /// <summary>
    /// Captain adds a player by name + optional email. Creates a Player record in the tournament.
    /// If email matches an existing User account the Player is linked to that account.
    /// </summary>
    Task<Player> AddPlayerManuallyAsync(Guid teamId, string name, string? email);

    /// <summary>Removes a player from their team slot (sets TeamId = null) while keeping them in the tournament.</summary>
    Task RemoveFromTeamAsync(Guid playerId);

    /// <summary>Transfers team captaincy to another current member of the same team.</summary>
    Task TransferCaptainAsync(Guid teamId, Guid newCaptainPlayerId);

    /// <summary>Captain edits a team member's faction and army list on their behalf.</summary>
    Task UpdateTeamMemberAsync(Guid playerId, string? armyFaction, string? armyList);
}
