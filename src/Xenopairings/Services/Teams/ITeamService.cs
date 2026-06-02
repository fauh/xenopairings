using Xenopairings.Models;

namespace Xenopairings.Services.Teams;

public interface ITeamService
{
    Task<Team> CreateTeamAsync(Guid tournamentId, string name, Guid captainPlayerId);
    Task<Team?> GetByInviteTokenAsync(string token);
    /// <summary>
    /// Adds a player to the team. Throws <see cref="InvalidOperationException"/> if the
    /// team is already full or the player is already on a team in this tournament.
    /// </summary>
    Task JoinTeamAsync(Guid teamId, Guid playerId);
    /// <summary>Returns all teams for the tournament with Players navigations loaded.</summary>
    Task<IReadOnlyList<Team>> ListByTournamentAsync(Guid tournamentId);
}
