using Microsoft.EntityFrameworkCore;
using Xenopairings.Data;
using Xenopairings.Models;
using Xenopairings.Services.Pairings;

namespace Xenopairings.Services.Elo;

/// <summary>
/// Global ELO rating service — snapshot model.
///
/// ELO updates only happen when a tournament is ended via ProcessTournamentAsync.
/// All matches in the tournament are evaluated against the player's pre-tournament rating
/// (the snapshot). Deltas from every match are summed and applied in one update.
///
/// Formula: standard Elo, starting rating 1000.
///   Expected:  Ea = 1 / (1 + 10^((Rb - Ra) / 400))
///   Delta:     ΔRa = K(games) × (Sa - Ea)
///   Net:       Ra' = Ra_snapshot + Σ ΔRa (across all matches in the tournament)
///
/// Variable K (calibration):
///   K(n) = max(K_MIN, K_START / (1 + n / K_HALF_LIFE))
///   where n = games played BEFORE this tournament.
///   0 games → K=64, 20 games → K=32, ~64 games → K=16 (floor).
///   New players calibrate quickly; established players are stable.
///
/// Outcome (Sa) — binary for both scoring systems:
///   Win (score > opponent, or GP > 10 for WTC) → Sa = 1.0
///   Draw (equal, or GP == 10 for WTC)          → Sa = 0.5
///   Loss                                        → Sa = 0.0
///   WTC game points determine the outcome category but are NOT used as
///   a fractional score — a 16–4 and a 12–8 are both wins (Sa = 1.0).
///
/// Byes and players without an email are skipped silently.
/// </summary>
public sealed class EloService(AppDbContext db) : IEloService
{
    private const double K_START     = 64.0;   // K for a brand-new player
    private const double K_MIN       = 16.0;   // K floor for experienced players
    private const double K_HALF_LIFE = 20.0;   // games played at which K halves from start to midpoint
    private const double InitialRating = 1000.0;

    /// <summary>
    /// Variable K-factor. Decreases smoothly as games played increases.
    ///   0 games  → K ≈ 64
    ///   20 games → K ≈ 32
    ///   64 games → K ≈ 16  (floor)
    /// </summary>
    private static double KFor(int gamesPlayed) =>
        Math.Max(K_MIN, K_START / (1.0 + gamesPlayed / K_HALF_LIFE));

    public async Task ProcessTournamentAsync(Guid tournamentId)
    {
        var tournament = await db.Tournaments.FindAsync(tournamentId);
        if (tournament is null) return;

        // Load all scored non-bye matches for this tournament, in round order
        var matches = await db.Matches
            .Include(m => m.Player1)
            .Include(m => m.Player2)
            .Include(m => m.Round)
            .Where(m => m.Round.TournamentId == tournamentId
                     && m.IsScored
                     && m.Player1Id != null
                     && m.Player2Id != null)
            .OrderBy(m => m.Round.RoundNumber)
            .ThenBy(m => m.TableNumber)
            .ToListAsync();

        if (matches.Count == 0) return;

        // Gather all unique player emails
        var emailSet = matches
            .SelectMany(m => new[]
            {
                m.Player1?.Email?.ToLowerInvariant(),
                m.Player2?.Email?.ToLowerInvariant(),
            })
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct()
            .ToHashSet()!;

        if (emailSet.Count == 0) return;

        // Snapshot: get-or-create PlayerRating for every involved player,
        //           record their pre-tournament rating.
        var ratingByEmail = new Dictionary<string, PlayerRating>(StringComparer.OrdinalIgnoreCase);
        var snapshotByEmail = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        // Snapshot games_played BEFORE this tournament to determine K for each player
        var snapshotGamesPlayed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var email in emailSet)
        {
            // Find the player name from the first match they appear in
            var name = matches
                .Select(m => m.Player1?.Email?.ToLowerInvariant() == email ? m.Player1?.Name
                           : m.Player2?.Email?.ToLowerInvariant() == email ? m.Player2?.Name
                           : null)
                .FirstOrDefault(n => n is not null) ?? email;

            var rating = await GetOrCreateAsync(email, name);
            ratingByEmail[email] = rating;
            snapshotByEmail[email] = rating.Rating;
            snapshotGamesPlayed[email] = rating.GamesPlayed;
        }

        // Calculate total ELO delta per player using the snapshot ratings
        var deltaByEmail = emailSet.ToDictionary(e => e, _ => 0.0, StringComparer.OrdinalIgnoreCase);
        var outcomesByEmail = new Dictionary<string, List<(Guid tournamentId, string opponentEmail, string opponentName, int myScore, int opponentScore, double actualOutcome)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in emailSet) outcomesByEmail[e] = [];

        foreach (var match in matches)
        {
            var p1Email = match.Player1?.Email?.ToLowerInvariant();
            var p2Email = match.Player2?.Email?.ToLowerInvariant();
            if (p1Email is null || p2Email is null) continue;
            if (!snapshotByEmail.ContainsKey(p1Email) || !snapshotByEmail.ContainsKey(p2Email)) continue;

            var (actual1, actual2) = ComputeOutcomes(match, tournament.ScoringSystem);

            // Use SNAPSHOT ratings for expected score calculation
            var snap1 = snapshotByEmail[p1Email];
            var snap2 = snapshotByEmail[p2Email];
            var expected1 = 1.0 / (1.0 + Math.Pow(10, (snap2 - snap1) / 400.0));
            var expected2 = 1.0 - expected1;

            // K is based on each player's games played BEFORE this tournament
            deltaByEmail[p1Email] += KFor(snapshotGamesPlayed[p1Email]) * (actual1 - expected1);
            deltaByEmail[p2Email] += KFor(snapshotGamesPlayed[p2Email]) * (actual2 - expected2);

            // Record outcome for history
            outcomesByEmail[p1Email].Add((tournamentId, p2Email, match.Player2?.Name ?? "?", match.Player1Score ?? 0, match.Player2Score ?? 0, actual1));
            outcomesByEmail[p2Email].Add((tournamentId, p1Email, match.Player1?.Name ?? "?", match.Player2Score ?? 0, match.Player1Score ?? 0, actual2));
        }

        // Apply deltas and write history
        foreach (var email in emailSet)
        {
            var rating = ratingByEmail[email];
            var snapshot = snapshotByEmail[email];
            var totalDelta = deltaByEmail[email];
            var newRating = snapshot + totalDelta;

            rating.DisplayName = matches
                .Select(m => m.Player1?.Email?.ToLowerInvariant() == email ? m.Player1?.Name
                           : m.Player2?.Email?.ToLowerInvariant() == email ? m.Player2?.Name
                           : null)
                .LastOrDefault(n => n is not null) ?? rating.DisplayName;
            rating.Rating = newRating;
            rating.GamesPlayed += outcomesByEmail[email].Count;
            rating.LastUpdated = DateTimeOffset.UtcNow;

            // One history entry per match, all sharing RatingBefore = snapshot, RatingAfter = newRating
            foreach (var (tid, oppEmail, oppName, myScore, oppScore, actual) in outcomesByEmail[email])
            {
                db.PlayerRatingHistories.Add(new PlayerRatingHistory
                {
                    Id = Guid.NewGuid(),
                    PlayerRatingId = rating.Id,
                    TournamentId = tid,
                    TournamentTitle = tournament.Title,
                    TournamentSlug = tournament.Slug,
                    OpponentEmail = oppEmail,
                    OpponentName = oppName,
                    MyRawScore = myScore,
                    OpponentRawScore = oppScore,
                    ActualOutcome = actual,
                    RatingBefore = snapshot,
                    RatingAfter = newRating,
                    PlayedAt = DateTimeOffset.UtcNow,
                });
            }
        }

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

    public async Task EnsureRatingAsync(string email, string displayName)
    {
        // GetOrCreateAsync only adds to the change tracker — call SaveChanges after.
        await GetOrCreateAsync(email, displayName);
        await db.SaveChangesAsync();
    }

    public async Task UpdateDisplayNameAsync(string email, string displayName)
    {
        var normalised = email.Trim().ToLowerInvariant();
        var rating = await db.PlayerRatings.FirstOrDefaultAsync(r => r.Email == normalised);
        if (rating is null) return;
        rating.DisplayName = displayName.Trim();
        await db.SaveChangesAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (double actual1, double actual2) ComputeOutcomes(
        Match match, ScoringSystem scoringSystem)
    {
        var raw1 = match.Player1Score ?? 0;
        var raw2 = match.Player2Score ?? 0;

        // WTC: convert to game points to determine the outcome category, then use binary
        // (a 16–4 and a 12–8 are both wins — ELO cares about win/draw/loss, not margin)
        if (scoringSystem == ScoringSystem.Wtc)
        {
            var (gp1, _) = WtcScoring.ConvertToGamePoints(raw1, raw2);
            if (gp1 > 10) return (1.0, 0.0);
            if (gp1 < 10) return (0.0, 1.0);
            return (0.5, 0.5);
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

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalised);

        var created = new PlayerRating
        {
            Id = Guid.NewGuid(),
            Email = normalised,
            DisplayName = displayName,
            Rating = InitialRating,
            GamesPlayed = 0,
            IsProfilePublic = true,
            IsVip = user?.IsVip ?? false,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUpdated = DateTimeOffset.UtcNow,
        };
        db.PlayerRatings.Add(created);
        return created;
    }
}
