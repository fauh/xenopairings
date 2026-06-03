using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xenopairings.Data;
using Xenopairings.Models;
using Xenopairings.Services.Elo;
using Xenopairings.Services.Email;

namespace Xenopairings.Services.Tournaments;

public class TournamentService(
    AppDbContext db,
    SlugGenerator slugGenerator,
    TokenGenerator tokenGenerator,
    IEmailSender emailSender,
    IOptions<EmailSettings> emailSettings,
    IEloService eloService,
    ILogger<TournamentService> logger) : ITournamentService
{
    private const int MaxSlugAttempts = 3;
    private readonly EmailSettings _emailSettings = emailSettings.Value;

    public async Task<Tournament> CreateAsync(CreateTournamentRequest request)
    {
        for (var attempt = 0; attempt < MaxSlugAttempts; attempt++)
        {
            var tournament = new Tournament
            {
                Id = Guid.NewGuid(),
                Slug = slugGenerator.Generate(request.Title),
                Title = request.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                StartsAt = TimeZoneHelper.ToUtc(request.StartsAt, request.TimeZoneId),
                TimeZoneId = request.TimeZoneId,
                NumberOfRounds = request.NumberOfRounds,
                MaxPlayers = request.MaxPlayers,
                OrganizerName = request.OrganizerName.Trim(),
                OrganizerEmail = request.OrganizerEmail.Trim().ToLowerInvariant(),
                ManageToken = tokenGenerator.RandomUrlSafeString(22),
                IsPrivate = request.IsPrivate,
                RegistrationOpen = request.RegistrationOpen,
                Status = TournamentStatus.Upcoming,
                ScoringSystem = request.ScoringSystem,
                IsTeamEvent = request.IsTeamEvent,
                TeamSize = request.IsTeamEvent ? request.TeamSize : null,
                TiebreakersJson = request.TiebreakersJson,
                TopCutSize = request.TopCutSize,
                CheckInEnabled = request.CheckInEnabled,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            try
            {
                db.Tournaments.Add(tournament);
                await db.SaveChangesAsync();

                await SendManageLinkEmailAsync(tournament);

                return tournament;
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex) && attempt < MaxSlugAttempts - 1)
            {
                db.ChangeTracker.Clear();
                logger.LogWarning("Slug or token collision on attempt {Attempt}, retrying.", attempt + 1);
            }
        }

        throw new InvalidOperationException("Could not generate a unique tournament slug after multiple attempts.");
    }

    public Task<Tournament?> GetByIdAsync(Guid id) =>
        db.Tournaments.FindAsync(id).AsTask();

    public Task<Tournament?> GetBySlugAsync(string slug) =>
        db.Tournaments.FirstOrDefaultAsync(t => t.Slug == slug);

    public Task<Tournament?> GetByManageTokenAsync(string token) =>
        db.Tournaments.FirstOrDefaultAsync(t => t.ManageToken == token);

    private async Task SendManageLinkEmailAsync(Tournament tournament)
    {
        var manageUrl = $"{_emailSettings.BaseUrl}/t/{tournament.Slug}/manage?t={tournament.ManageToken}";
        var publicUrl = $"{_emailSettings.BaseUrl}/t/{tournament.Slug}";
        var subject = $"Your manage link for \"{tournament.Title}\"";
        var body = $"""
            Hi {tournament.OrganizerName},

            Your tournament "{tournament.Title}" has been created.

            Manage your tournament here (save this link — it's your only way back in):
            {manageUrl}

            Share the public URL with players:
            {publicUrl}
            """;

        try
        {
            await emailSender.SendAsync(tournament.OrganizerEmail, subject, body);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to send manage-link email to {Email} for tournament {TournamentId}.",
                tournament.OrganizerEmail, tournament.Id);
        }
    }

    public async Task<IReadOnlyList<Tournament>> ListPublicAsync()
    {
        // DateTimeOffset cannot be used in ORDER BY by EF Core's SQLite provider —
        // fetch unordered then sort in memory (same pattern as PlayerService).
        var tournaments = await db.Tournaments
            .Where(t => !t.IsPrivate)
            .ToListAsync();
        return [.. tournaments.OrderByDescending(t => t.StartsAt)];
    }

    public async Task<IReadOnlyList<Tournament>> ListAllAsync()
    {
        var tournaments = await db.Tournaments.ToListAsync();
        return [.. tournaments.OrderByDescending(t => t.StartsAt)];
    }

    public async Task<IReadOnlyList<Tournament>> ListByOrganizerEmailAsync(string email)
    {
        var normalised = email.Trim().ToLowerInvariant();
        var tournaments = await db.Tournaments
            .Where(t => t.OrganizerEmail == normalised)
            .ToListAsync();
        return [.. tournaments.OrderByDescending(t => t.StartsAt)];
    }

    public async Task SetRegistrationOpenAsync(Guid tournamentId, bool open)
    {
        var tournament = await db.Tournaments.FindAsync(tournamentId);
        if (tournament is null) return;
        tournament.RegistrationOpen = open;
        await db.SaveChangesAsync();
    }

    public async Task SetCheckInEnabledAsync(Guid tournamentId, bool enabled)
    {
        var t = await db.Tournaments.FindAsync(tournamentId);
        if (t is null) return;
        t.CheckInEnabled = enabled;
        await db.SaveChangesAsync();
    }

    public async Task SetArmyListLockedAsync(Guid tournamentId, bool locked)
    {
        var t = await db.Tournaments.FindAsync(tournamentId);
        if (t is null) return;
        t.ArmyListLocked = locked;
        await db.SaveChangesAsync();
    }

    public async Task StartAsync(Guid tournamentId)
    {
        var t = await db.Tournaments.FindAsync(tournamentId)
            ?? throw new InvalidOperationException("Tournament not found.");
        if (t.Status != TournamentStatus.Upcoming)
            throw new InvalidOperationException("Only an upcoming tournament can be started.");
        t.Status = TournamentStatus.InProgress;
        t.RegistrationOpen = false;
        await db.SaveChangesAsync();
    }

    public async Task EndAsync(Guid tournamentId)
    {
        var t = await db.Tournaments.FindAsync(tournamentId)
            ?? throw new InvalidOperationException("Tournament not found.");
        if (t.Status == TournamentStatus.Ended)
            throw new InvalidOperationException("Tournament is already ended.");
        t.Status = TournamentStatus.Ended;
        t.RegistrationOpen = false;
        await db.SaveChangesAsync();

        // Process ELO for all scored matches in the tournament (snapshot model).
        // Best-effort — don't fail the end-tournament action if ELO calculation errors.
        try { await eloService.ProcessTournamentAsync(tournamentId); }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "ELO processing failed for tournament {TournamentId} — ratings may be incomplete.",
                tournamentId);
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("UNIQUE") == true;
}
