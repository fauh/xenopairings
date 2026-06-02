using Microsoft.EntityFrameworkCore;
using Xenopairings.Models;
using Xenopairings.Services.Elo;
using Xenopairings.Tests.Infrastructure;
using Shouldly;

namespace Xenopairings.Tests.Services;

public class EloServiceTests : IClassFixture<InMemoryDatabaseFixture>
{
    private readonly InMemoryDatabaseFixture _db;

    public EloServiceTests(InMemoryDatabaseFixture db) => _db = db;

    private EloService BuildSut() => new(_db.CreateDbContext());

    // Seeds a minimal tournament + 2 players + 1 scored match. Returns the match ID.
    private async Task<Guid> SeedScoredMatchAsync(
        string p1Email, string p2Email,
        int p1Score, int p2Score,
        ScoringSystem scoring = ScoringSystem.Gw)
    {
        await using var ctx = _db.CreateDbContext();
        var tid = Guid.NewGuid();
        var rid = Guid.NewGuid();
        var mid = Guid.NewGuid();
        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();

        ctx.Tournaments.Add(new Tournament
        {
            Id = tid, Slug = $"elo-{Guid.NewGuid():N}", Title = "ELO Test",
            StartsAt = DateTimeOffset.UtcNow, TimeZoneId = "UTC",
            NumberOfRounds = 3, MaxPlayers = 8,
            OrganizerName = "TO", OrganizerEmail = "to@test.com",
            ManageToken = Guid.NewGuid().ToString("N"), CreatedAt = DateTimeOffset.UtcNow,
            ScoringSystem = scoring,
        });
        ctx.Players.Add(new Player
        {
            Id = p1Id, TournamentId = tid, Name = "Alice", Email = p1Email,
            EditToken = Guid.NewGuid().ToString("N"), RegisteredAt = DateTimeOffset.UtcNow,
        });
        ctx.Players.Add(new Player
        {
            Id = p2Id, TournamentId = tid, Name = "Bob", Email = p2Email,
            EditToken = Guid.NewGuid().ToString("N"), RegisteredAt = DateTimeOffset.UtcNow,
        });
        ctx.Rounds.Add(new Round
        {
            Id = rid, TournamentId = tid, RoundNumber = 1,
            IsComplete = false, CreatedAt = DateTimeOffset.UtcNow,
        });
        ctx.Matches.Add(new Match
        {
            Id = mid, RoundId = rid, TableNumber = 1,
            Player1Id = p1Id, Player2Id = p2Id,
            Player1Score = p1Score, Player2Score = p2Score,
            IsScored = true,
        });
        await ctx.SaveChangesAsync();
        return mid;
    }

    // ── Win updates ratings correctly (GW) ────────────────────────────────────

    [Fact]
    public async Task UpdateMatchRatings_GwWin_RaisesWinnerLowersLoser()
    {
        var mid = await SeedScoredMatchAsync("alice@test.com", "bob@test.com", 87, 42);
        await BuildSut().UpdateMatchRatingsAsync(mid);

        await using var ctx = _db.CreateDbContext();
        var alice = await ctx.PlayerRatings.SingleAsync(r => r.Email == "alice@test.com");
        var bob   = await ctx.PlayerRatings.SingleAsync(r => r.Email == "bob@test.com");

        alice.Rating.ShouldBeGreaterThan(1000);
        bob.Rating.ShouldBeLessThan(1000);
        (alice.Rating + bob.Rating).ShouldBe(2000, tolerance: 0.001);  // zero-sum
    }

    // ── Draw: both stay near 1000 ─────────────────────────────────────────────

    [Fact]
    public async Task UpdateMatchRatings_GwDraw_BothStayNear1000()
    {
        var mid = await SeedScoredMatchAsync("draw1@test.com", "draw2@test.com", 50, 50);
        await BuildSut().UpdateMatchRatingsAsync(mid);

        await using var ctx = _db.CreateDbContext();
        var r1 = await ctx.PlayerRatings.SingleAsync(r => r.Email == "draw1@test.com");
        var r2 = await ctx.PlayerRatings.SingleAsync(r => r.Email == "draw2@test.com");

        r1.Rating.ShouldBe(1000, tolerance: 0.001);
        r2.Rating.ShouldBe(1000, tolerance: 0.001);
    }

    // ── WTC fractional outcome ────────────────────────────────────────────────

    [Fact]
    public async Task UpdateMatchRatings_WtcDiff30_UsesFractionalOutcome()
    {
        // diff 30 → 16/4 game points. p1 gets 16/20 = 0.8 outcome
        var mid = await SeedScoredMatchAsync(
            "wtc1@test.com", "wtc2@test.com", 80, 50,
            ScoringSystem.Wtc);
        await BuildSut().UpdateMatchRatingsAsync(mid);

        await using var ctx = _db.CreateDbContext();
        var r1 = await ctx.PlayerRatings.SingleAsync(r => r.Email == "wtc1@test.com");
        var r2 = await ctx.PlayerRatings.SingleAsync(r => r.Email == "wtc2@test.com");

        r1.Rating.ShouldBeGreaterThan(1000);
        r2.Rating.ShouldBeLessThan(1000);
        // A 16/4 WTC result gives less than a full win (20/0)
        r1.Rating.ShouldBeLessThan(1016);  // max K gain for full win ≈ 16
    }

    // ── Bye match: no rating update ───────────────────────────────────────────

    [Fact]
    public async Task UpdateMatchRatings_ByeMatch_NoRatingCreated()
    {
        await using var ctx = _db.CreateDbContext();
        var tid = Guid.NewGuid();
        var rid = Guid.NewGuid();
        var mid = Guid.NewGuid();
        var p1Id = Guid.NewGuid();

        ctx.Tournaments.Add(new Tournament
        {
            Id = tid, Slug = $"bye-elo-{Guid.NewGuid():N}", Title = "Bye",
            StartsAt = DateTimeOffset.UtcNow, TimeZoneId = "UTC",
            NumberOfRounds = 1, MaxPlayers = 4,
            OrganizerName = "TO", OrganizerEmail = "to@test.com",
            ManageToken = Guid.NewGuid().ToString("N"), CreatedAt = DateTimeOffset.UtcNow,
        });
        ctx.Players.Add(new Player
        {
            Id = p1Id, TournamentId = tid, Name = "Solo", Email = "solo@test.com",
            EditToken = Guid.NewGuid().ToString("N"), RegisteredAt = DateTimeOffset.UtcNow,
        });
        ctx.Rounds.Add(new Round
        {
            Id = rid, TournamentId = tid, RoundNumber = 1,
            IsComplete = false, CreatedAt = DateTimeOffset.UtcNow,
        });
        ctx.Matches.Add(new Match
        {
            Id = mid, RoundId = rid, TableNumber = 1,
            Player1Id = p1Id, Player2Id = null,  // bye
            IsScored = true,
        });
        await ctx.SaveChangesAsync();

        await BuildSut().UpdateMatchRatingsAsync(mid);

        await using var ctx2 = _db.CreateDbContext();
        var rating = await ctx2.PlayerRatings.FirstOrDefaultAsync(r => r.Email == "solo@test.com");
        rating.ShouldBeNull();
    }

    // ── GamesPlayed increments ────────────────────────────────────────────────

    [Fact]
    public async Task UpdateMatchRatings_IncrementsGamesPlayed()
    {
        var mid = await SeedScoredMatchAsync("gp1@test.com", "gp2@test.com", 60, 40);
        await BuildSut().UpdateMatchRatingsAsync(mid);

        await using var ctx = _db.CreateDbContext();
        var r1 = await ctx.PlayerRatings.SingleAsync(r => r.Email == "gp1@test.com");
        r1.GamesPlayed.ShouldBe(1);
    }

    // ── Zero-sum property ─────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateMatchRatings_RatingChangesAreZeroSum()
    {
        var mid = await SeedScoredMatchAsync("zs1@test.com", "zs2@test.com", 100, 0);
        await BuildSut().UpdateMatchRatingsAsync(mid);

        await using var ctx = _db.CreateDbContext();
        var r1 = await ctx.PlayerRatings.SingleAsync(r => r.Email == "zs1@test.com");
        var r2 = await ctx.PlayerRatings.SingleAsync(r => r.Email == "zs2@test.com");
        (r1.Rating + r2.Rating).ShouldBe(2000, tolerance: 0.001);
    }

    // ── Leaderboard sorted by rating desc ─────────────────────────────────────

    [Fact]
    public async Task GetLeaderboardAsync_SortsByRatingDesc()
    {
        var mid = await SeedScoredMatchAsync("lb1@test.com", "lb2@test.com", 90, 10);
        await BuildSut().UpdateMatchRatingsAsync(mid);

        var leaderboard = await BuildSut().GetLeaderboardAsync();
        leaderboard.Count.ShouldBeGreaterThanOrEqualTo(2);
        for (var i = 0; i < leaderboard.Count - 1; i++)
            leaderboard[i].Rating.ShouldBeGreaterThanOrEqualTo(leaderboard[i + 1].Rating);
    }
}
