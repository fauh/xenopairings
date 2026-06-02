using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xenopairings.Data;
using Xenopairings.Models;
using Xenopairings.Services.Email;

namespace Xenopairings.Services.Tournaments;

public class TournamentService(
    AppDbContext db,
    SlugGenerator slugGenerator,
    TokenGenerator tokenGenerator,
    IEmailSender emailSender,
    IOptions<EmailSettings> emailSettings,
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

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("UNIQUE") == true;
}
