using Xenopairings.Models;

namespace Xenopairings.Services.Tournaments;

public interface ITournamentService
{
    Task<Tournament> CreateAsync(CreateTournamentRequest request);
    Task<Tournament?> GetByIdAsync(Guid id);
    Task<Tournament?> GetBySlugAsync(string slug);
    Task<Tournament?> GetByManageTokenAsync(string token);
    Task SetRegistrationOpenAsync(Guid tournamentId, bool open);
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
    bool RegistrationOpen = true
);
