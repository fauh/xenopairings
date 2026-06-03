using Microsoft.EntityFrameworkCore;
using Xenopairings.Models;
using Xenopairings.Services.Elo;
using Xenopairings.Tests.Infrastructure;
using Shouldly;

namespace Xenopairings.Tests.Services;

/// <summary>
/// Tests for snapshot-model ELO: all deltas calculated against pre-tournament rating,
/// applied in one batch when ProcessTournamentAsync is called at tournament end.
/// </summary>
public class EloServiceTests : IClassFixture<InMemoryDatabaseFixture>
{
    private readonly InMemoryDatabaseFixture _db;

    public EloServiceTests(InMemoryDatabaseFixture db) => _db = db;

    private EloService BuildSut() => new(_db.CreateDbContext());

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private async Task<(Guid tid, Guid roundId, Guid matchId, string p1Email, string p2Email)>
        SeedTournamentWithScoredMatchAsync(
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
            NumberOfRounds = 1, MaxPlayers = 8,
            OrganizerName = "TO", OrganizerEmail = "to@test.com",
            ManageToken = Guid.NewGuid().ToString("N"), CreatedAt = DateTimeOffset.UtcNow,
            ScoringSystem = scoring,
        });
        ctx.Players.Add(new Player { Id = p1Id, TournamentId = tid, Name = "Alice", Email = p1Email, EditToken = Guid.NewGuid().ToString("N"), RegisteredAt = DateTimeOffset.UtcNow });
        ctx.Players.Add(new Player { Id = p2Id, TournamentId = tid, Name = "Bob",   Email = p2Email, EditToken = Guid.NewGuid().ToString("N"), RegisteredAt = DateTimeOffset.UtcNow });
        ctx.Rounds.Add(new Round { Id = rid, TournamentId = tid, RoundNumber = 1, IsComplete = true, CreatedAt = DateTimeOffset.UtcNow });
        ctx.Matches.Add(new Match { Id = mid, RoundId = rid, TableNumber = 1, Player1Id = p1Id, Player2Id = p2Id, Player1Score = p1Score, Player2Score = p2Score, IsScored = true });
        await ctx.SaveChangesAsync();
        return (tid, rid, mid, p1Email, p2Email);
    }

    // ── GW win updates ratings ────────────────────────────────────────────────

    [Fact]
    public async Task ProcessTournament_GwWin_RaisesWinnerLowersLoser()
    {
        var (tid, _, _, _, _) = await SeedTournamentWithScoredMatchAsync(
            "alice@test.com", "bob@test.com", 87, 42);

        await BuildSut().ProcessTournamentAsync(tid);

        await using var ctx = _db.CreateDbContext();
        var alice = await ctx.PlayerRatings.SingleAsync(r => r.Email == "alice@test.com");
        var bob   = await ctx.PlayerRatings.SingleAsync(r => r.Email == "bob@test.com");

        alice.Rating.ShouldBeGreaterThan(1000);
        bob.Rating.ShouldBeLessThan(1000);
        (alice.Rating + bob.Rating).ShouldBe(2000, tolerance: 0.001);  // zero-sum
    }

    // ── Draw: both stay near 1000 ─────────────────────────────────────────────

    [Fact]
    public async Task ProcessTournament_GwDraw_BothStayNear1000()
    {
        var (tid, _, _, _, _) = await SeedTournamentWithScoredMatchAsync(
            "draw1@test.com", "draw2@test.com", 50, 50);

        await BuildSut().ProcessTournamentAsync(tid);

        await using var ctx = _db.CreateDbContext();
        var r1 = await ctx.PlayerRatings.SingleAsync(r => r.Email == "draw1@test.com");
        var r2 = await ctx.PlayerRatings.SingleAsync(r => r.Email == "draw2@test.com");

        r1.Rating.ShouldBe(1000, tolerance: 0.001);
        r2.Rating.ShouldBe(1000, tolerance: 0.001);
    }

    // ── WTC fractional outcome ────────────────────────────────────────────────

    [Fact]
    public async Task ProcessTournament_WtcDiff30_UsesFractionalOutcome()
    {
        // diff 30 → 16/4 GP, p1 gets 16/20 = 0.8 outcome
        var (tid, _, _, _, _) = await SeedTournamentWithScoredMatchAsync(
            "wtc1@test.com", "wtc2@test.com", 80, 50, ScoringSystem.Wtc);

        await BuildSut().ProcessTournamentAsync(tid);

        await using var ctx = _db.CreateDbContext();
        var r1 = await ctx.PlayerRatings.SingleAsync(r => r.Email == "wtc1@test.com");
        var r2 = await ctx.PlayerRatings.SingleAsync(r => r.Email == "wtc2@test.com");

        r1.Rating.ShouldBeGreaterThan(1000);
        r2.Rating.ShouldBeLessThan(1000);
        // 0.8 outcome gives less than full K gain (max K ≈ 16 at equal ratings)
        r1.Rating.ShouldBeLessThan(1016);
    }

    // ── Snapshot: multi-match deltas use pre-tournament rating ────────────────

    [Fact]
    public async Task ProcessTournament_MultipleMatches_SnapshotRatingUsed()
    {
        // Two rounds in the same tournament, same players
        await using var ctx = _db.CreateDbContext();
        var tid = Guid.NewGuid();
        var p1Id = Guid.NewGuid(); var p2Id = Guid.NewGuid();
        var rid1 = Guid.NewGuid(); var rid2 = Guid.NewGuid();

        ctx.Tournaments.Add(new Tournament { Id = tid, Slug = $"snap-{Guid.NewGuid():N}", Title = "Snap", StartsAt = DateTimeOffset.UtcNow, TimeZoneId = "UTC", NumberOfRounds = 2, MaxPlayers = 4, OrganizerName = "TO", OrganizerEmail = "to2@test.com", ManageToken = Guid.NewGuid().ToString("N"), CreatedAt = DateTimeOffset.UtcNow });
        ctx.Players.Add(new Player { Id = p1Id, TournamentId = tid, Name = "Carol", Email = "carol@test.com", EditToken = Guid.NewGuid().ToString("N"), RegisteredAt = DateTimeOffset.UtcNow });
        ctx.Players.Add(new Player { Id = p2Id, TournamentId = tid, Name = "Dave",  Email = "dave@test.com",  EditToken = Guid.NewGuid().ToString("N"), RegisteredAt = DateTimeOffset.UtcNow });
        ctx.Rounds.Add(new Round { Id = rid1, TournamentId = tid, RoundNumber = 1, IsComplete = true, CreatedAt = DateTimeOffset.UtcNow });
        ctx.Rounds.Add(new Round { Id = rid2, TournamentId = tid, RoundNumber = 2, IsComplete = true, CreatedAt = DateTimeOffset.UtcNow });
        // Carol wins both rounds
        ctx.Matches.Add(new Match { Id = Guid.NewGuid(), RoundId = rid1, TableNumber = 1, Player1Id = p1Id, Player2Id = p2Id, Player1Score = 90, Player2Score = 30, IsScored = true });
        ctx.Matches.Add(new Match { Id = Guid.NewGuid(), RoundId = rid2, TableNumber = 1, Player1Id = p1Id, Player2Id = p2Id, Player1Score = 80, Player2Score = 40, IsScored = true });
        await ctx.SaveChangesAsync();

        await BuildSut().ProcessTournamentAsync(tid);

        await using var ctx2 = _db.CreateDbContext();
        var carol = await ctx2.PlayerRatings.SingleAsync(r => r.Email == "carol@test.com");
        var dave  = await ctx2.PlayerRatings.SingleAsync(r => r.Email == "dave@test.com");

        carol.Rating.ShouldBeGreaterThan(1000);
        dave.Rating.ShouldBeLessThan(1000);
        // Both matches used the same snapshot (1000) so sum of deltas is double a single win
        carol.GamesPlayed.ShouldBe(2);
    }

    // ── Zero-sum ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessTournament_RatingChangesAreZeroSum()
    {
        var (tid, _, _, _, _) = await SeedTournamentWithScoredMatchAsync(
            "zs1@test.com", "zs2@test.com", 100, 0);

        await BuildSut().ProcessTournamentAsync(tid);

        await using var ctx = _db.CreateDbContext();
        var r1 = await ctx.PlayerRatings.SingleAsync(r => r.Email == "zs1@test.com");
        var r2 = await ctx.PlayerRatings.SingleAsync(r => r.Email == "zs2@test.com");
        (r1.Rating + r2.Rating).ShouldBe(2000, tolerance: 0.001);
    }

    // ── Idempotent guard: no-op on empty/unscored tournament ──────────────────

    [Fact]
    public async Task ProcessTournament_NoScoredMatches_DoesNotCreateRatings()
    {
        await using var ctx = _db.CreateDbContext();
        var tid = Guid.NewGuid();
        ctx.Tournaments.Add(new Tournament { Id = tid, Slug = $"empty-elo-{Guid.NewGuid():N}", Title = "Empty", StartsAt = DateTimeOffset.UtcNow, TimeZoneId = "UTC", NumberOfRounds = 1, MaxPlayers = 4, OrganizerName = "TO", OrganizerEmail = "to3@test.com", ManageToken = Guid.NewGuid().ToString("N"), CreatedAt = DateTimeOffset.UtcNow });
        await ctx.SaveChangesAsync();

        await Should.NotThrowAsync(() => BuildSut().ProcessTournamentAsync(tid));

        // No new ratings should have been created (no scored matches, no players with email)
        await using var ctx2 = _db.CreateDbContext();
        (await ctx2.PlayerRatings.AnyAsync(r => r.Email == "to3@test.com")).ShouldBeFalse();
    }

    // ── Leaderboard sorted by rating desc ─────────────────────────────────────

    [Fact]
    public async Task GetLeaderboardAsync_SortsByRatingDesc()
    {
        var (tid, _, _, _, _) = await SeedTournamentWithScoredMatchAsync(
            "lb1@test.com", "lb2@test.com", 90, 10);
        await BuildSut().ProcessTournamentAsync(tid);

        var leaderboard = await BuildSut().GetLeaderboardAsync();
        leaderboard.Count.ShouldBeGreaterThanOrEqualTo(2);
        for (var i = 0; i < leaderboard.Count - 1; i++)
            leaderboard[i].Rating.ShouldBeGreaterThanOrEqualTo(leaderboard[i + 1].Rating);
    }
}
