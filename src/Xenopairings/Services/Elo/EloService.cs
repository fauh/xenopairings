using Microsoft.EntityFrameworkCore;
using Xenopairings.Data;
using Xenopairings.Models;
using Xenopairings.Services.Pairings;

namespace Xenopairings.Services.Elo;

/// <summary>
/// Global ELO rating service.
///
/// Formula: standard Elo with K = 32 and starting rating 1000.
///   Expected:  Ea = 1 / (1 + 10^((Rb - Ra) / 400))
///   New:       Ra' = Ra + K * (Sa - Ea)
///
/// Outcome (Sa) per scoring system:
///   GW:  win=1.0 · draw=0.5 · loss=0.0
///   WTC: Sa = player_game_points / 20.0  (fractional; both players' Sa sum to 1)
///
/// Byes and players without an email are skipped silently.
/// A <see cref="PlayerRatingHistory"/> row is written for each participant after each rated match.
/// </summary>
public sealed class EloService(AppDbContext db) : IEloService
{
    private const double K = 32.0;
    private const double InitialRating = 1000.0;

    public async Task UpdateMatchRatingsAsync(Guid matchId)
    {
        var match = await db.Matches
            .Include(m => m.Player1)
            .Include(m => m.Player2)
            .Include(m => m.Round)
                .ThenInclude(r => r.Tournament)
            .FirstOrDefaultAsync(m => m.Id == matchId);

        if (match is null || !match.IsScored) return;
        if (match.Player2Id is null) return;  // bye

        var p1 = match.Player1;
        var p2 = match.Player2;
        if (p1 is null || p2 is null) return;
        if (string.IsNullOrWhiteSpace(p1.Email) || string.IsNullOrWhiteSpace(p2.Email)) return;

        var tournament = match.Round.Tournament;
        var (actual1, actual2) = ComputeOutcomes(match, tournament.ScoringSystem);

        var rating1 = await GetOrCreateAsync(p1.Email, p1.Name);
        var rating2 = await GetOrCreateAsync(p2.Email, p2.Name);

        rating1.DisplayName = p1.Name;
        rating2.DisplayName = p2.Name;

        var before1 = rating1.Rating;
        var before2 = rating2.Rating;

        var expected1 = 1.0 / (1.0 + Math.Pow(10, (before2 - before1) / 400.0));
        var expected2 = 1.0 - expected1;

        rating1.Rating = before1 + K * (actual1 - expected1);
        rating2.Rating = before2 + K * (actual2 - expected2);
        rating1.GamesPlayed++;
        rating2.GamesPlayed++;
        rating1.LastUpdated = DateTimeOffset.UtcNow;
        rating2.LastUpdated = DateTimeOffset.UtcNow;

        // History for player 1
        db.PlayerRatingHistories.Add(new PlayerRatingHistory
        {
            Id = Guid.NewGuid(),
            PlayerRatingId = rating1.Id,
            TournamentId = tournament.Id,
            TournamentTitle = tournament.Title,
            TournamentSlug = tournament.Slug,
            OpponentName = p2.Name,
            OpponentEmail = p2.Email?.ToLowerInvariant(),
            MyRawScore = match.Player1Score ?? 0,
            OpponentRawScore = match.Player2Score ?? 0,
            ActualOutcome = actual1,
            RatingBefore = before1,
            RatingAfter = rating1.Rating,
            PlayedAt = DateTimeOffset.UtcNow,
        });

        // History for player 2
        db.PlayerRatingHistories.Add(new PlayerRatingHistory
        {
            Id = Guid.NewGuid(),
            PlayerRatingId = rating2.Id,
            TournamentId = tournament.Id,
            TournamentTitle = tournament.Title,
            TournamentSlug = tournament.Slug,
            OpponentName = p1.Name,
            OpponentEmail = p1.Email?.ToLowerInvariant(),
            MyRawScore = match.Player2Score ?? 0,
            OpponentRawScore = match.Player1Score ?? 0,
            ActualOutcome = actual2,
            RatingBefore = before2,
            RatingAfter = rating2.Rating,
            PlayedAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<PlayerRating>> GetLeaderboardAsync()
    {
        var ratings = await db.PlayerRatings.ToListAsync();
        return [.. ratings.OrderByDescending(r => r.Rating)];
    }

    public Task<PlayerRating?> GetByEmailAsync(string email)
    {
        var normalised = email.Trim().ToLowerInvariant();
        return db.PlayerRatings.FirstOrDefaultAsync(r => r.Email == normalised);
    }

    public async Task<(PlayerRating Rating, IReadOnlyList<PlayerRatingHistory> History)?> GetProfileAsync(
        Guid ratingId)
    {
        var rating = await db.PlayerRatings.FindAsync(ratingId);
        if (rating is null) return null;

        var history = await db.PlayerRatingHistories
            .Where(h => h.PlayerRatingId == ratingId)
            .ToListAsync();

        // Sort in memory to avoid DateTimeOffset SQL translation issues
        var sorted = history.OrderBy(h => h.PlayedAt).ToList();

        return (rating, sorted);
    }

    public async Task SetProfileVisibilityAsync(Guid ratingId, bool isPublic)
    {
        var rating = await db.PlayerRatings.FindAsync(ratingId);
        if (rating is null) return;
        rating.IsProfilePublic = isPublic;
        await db.SaveChangesAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (double actual1, double actual2) ComputeOutcomes(
        Match match, ScoringSystem scoringSystem)
    {
        var raw1 = match.Player1Score ?? 0;
        var raw2 = match.Player2Score ?? 0;

        if (scoringSystem == ScoringSystem.Wtc)
        {
            var (gp1, gp2) = WtcScoring.ConvertToGamePoints(raw1, raw2);
            return (gp1 / 20.0, gp2 / 20.0);
        }

        if (raw1 > raw2) return (1.0, 0.0);
        if (raw1 < raw2) return (0.0, 1.0);
        return (0.5, 0.5);
    }

    private async Task<PlayerRating> GetOrCreateAsync(string email, string displayName)
    {
        var normalised = email.Trim().ToLowerInvariant();
        var existing = await db.PlayerRatings
            .FirstOrDefaultAsync(r => r.Email == normalised);

        if (existing is not null) return existing;

        var created = new PlayerRating
        {
            Id = Guid.NewGuid(),
            Email = normalised,
            DisplayName = displayName,
            Rating = InitialRating,
            GamesPlayed = 0,
            IsProfilePublic = true,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUpdated = DateTimeOffset.UtcNow,
        };
        db.PlayerRatings.Add(created);
        return created;
    }
}
