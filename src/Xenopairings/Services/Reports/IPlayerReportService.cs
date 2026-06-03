using Xenopairings.Models;

namespace Xenopairings.Services.Reports;

public interface IPlayerReportService
{
    /// <summary>
    /// Files a report against a player in a tournament.
    /// Sends an email to the tournament organizer.
    /// </summary>
    Task<PlayerReport> FileReportAsync(
        Guid tournamentId,
        Guid? reporterPlayerId,
        Guid reportedPlayerId,
        string reason);

    Task<IReadOnlyList<PlayerReport>> ListByTournamentAsync(Guid tournamentId);

    Task ResolveAsync(Guid reportId, string? organizerNote);
}
