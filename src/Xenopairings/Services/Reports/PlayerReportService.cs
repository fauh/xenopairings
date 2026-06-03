using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xenopairings.Data;
using Xenopairings.Models;
using Xenopairings.Services.Email;

namespace Xenopairings.Services.Reports;

public sealed class PlayerReportService(
    AppDbContext db,
    IEmailSender emailSender,
    IOptions<EmailSettings> emailSettings,
    ILogger<PlayerReportService> logger) : IPlayerReportService
{
    public async Task<PlayerReport> FileReportAsync(
        Guid tournamentId,
        Guid? reporterPlayerId,
        Guid reportedPlayerId,
        string reason)
    {
        var tournament = await db.Tournaments.FindAsync(tournamentId)
            ?? throw new InvalidOperationException("Tournament not found.");

        var report = new PlayerReport
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            ReporterPlayerId = reporterPlayerId,
            ReportedPlayerId = reportedPlayerId,
            Reason = reason.Trim(),
            FiledAt = DateTimeOffset.UtcNow,
        };

        db.PlayerReports.Add(report);
        await db.SaveChangesAsync();

        // Email the organizer
        _ = Task.Run(() => NotifyOrganizerAsync(tournament, report, reporterPlayerId, reportedPlayerId));

        return report;
    }

    public async Task<IReadOnlyList<PlayerReport>> ListByTournamentAsync(Guid tournamentId)
    {
        var reports = await db.PlayerReports
            .Include(r => r.ReporterPlayer)
            .Include(r => r.ReportedPlayer)
            .Where(r => r.TournamentId == tournamentId)
            .ToListAsync();
        return [.. reports.OrderByDescending(r => r.FiledAt)];
    }

    public async Task ResolveAsync(Guid reportId, string? organizerNote)
    {
        var report = await db.PlayerReports.FindAsync(reportId);
        if (report is null) return;
        report.IsResolved = true;
        report.OrganizerNote = organizerNote?.Trim();
        await db.SaveChangesAsync();
    }

    private async Task NotifyOrganizerAsync(
        Tournament tournament,
        PlayerReport report,
        Guid? reporterPlayerId,
        Guid reportedPlayerId)
    {
        try
        {
            var reported = await db.Players.FindAsync(reportedPlayerId);
            var reporter = reporterPlayerId.HasValue
                ? await db.Players.FindAsync(reporterPlayerId.Value)
                : null;

            var baseUrl = emailSettings.Value.BaseUrl.TrimEnd('/');
            var manageUrl = $"{baseUrl}/t/{tournament.Slug}/manage";

            var body = $"""
                Hi {tournament.OrganizerName},

                A player has been reported in your tournament "{tournament.Title}".

                Reported player: {reported?.Name ?? "unknown"}
                Reported by:     {reporter?.Name ?? "anonymous"}
                Reason:
                {report.Reason}

                View and manage reports:
                {manageUrl}
                """;

            await emailSender.SendAsync(
                tournament.OrganizerEmail,
                $"Player reported in \"{tournament.Title}\"",
                body);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send report notification for tournament {TournamentId}.", tournament.Id);
        }
    }
}
