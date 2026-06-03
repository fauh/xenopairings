using Xenopairings.Models;

namespace Xenopairings.Services.Tournaments;

public interface ITournamentService
{
    Task<Tournament> CreateAsync(CreateTournamentRequest request);
    Task<Tournament?> GetByIdAsync(Guid id);
    Task<Tournament?> GetBySlugAsync(string slug);
    Task<Tournament?> GetByManageTokenAsync(string token);
    Task SetRegistrationOpenAsync(Guid tournamentId, bool open);
    Task SetArmyListLockedAsync(Guid tournamentId, bool locked);
    /// <summary>Transitions Upcoming → InProgress; auto-closes registration.</summary>
    Task StartAsync(Guid tournamentId);
    /// <summary>Transitions any non-Ended status → Ended; auto-closes registration.</summary>
    Task EndAsync(Guid tournamentId);
    /// <summary>Returns all non-private tournaments, newest first.</summary>
    Task<IReadOnlyList<Tournament>> ListPublicAsync();
    /// <summary>Returns ALL tournaments (including private), newest first. Admin use only.</summary>
    Task<IReadOnlyList<Tournament>> ListAllAsync();
    /// <summary>Returns tournaments where OrganizerEmail matches, newest first.</summary>
    Task<IReadOnlyList<Tournament>> ListByOrganizerEmailAsync(string email);
}

public record CreateTournamentRequest(
    string Title,
    string? Description,
    DateTime StartsAt,
    string TimeZoneId,
    int NumberOfRounds,
    int MaxPlayers,
    string OrganizerName,
    string OrganizerEmail,
    bool IsPrivate = false,
    bool RegistrationOpen = true,
    ScoringSystem ScoringSystem = ScoringSystem.Gw,
    bool IsTeamEvent = false,
    int? TeamSize = null
);
