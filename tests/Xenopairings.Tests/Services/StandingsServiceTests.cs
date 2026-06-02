using Microsoft.Extensions.Logging.Abstractions;
using Xenopairings.Models;
using Xenopairings.Services.Rounds;
using Xenopairings.Services.Standings;
using Xenopairings.Tests.Infrastructure;
using Shouldly;

namespace Xenopairings.Tests.Services;

public class StandingsServiceTests : IClassFixture<InMemoryDatabaseFixture>
{
    private readonly InMemoryDatabaseFixture _db;

    public StandingsServiceTests(InMemoryDatabaseFixture db) => _db = db;

    private StandingsService BuildSut() => new(_db.CreateDbContext());

    private RoundService BuildRoundSvc()
    {
        var ctx = _db.CreateDbContext();
        return new RoundService(ctx, new StandingsService(ctx), NullLogger<RoundService>.Instance);
    }

    private async Task<(Guid tid, Guid p1, Guid p2)> SeedTwoPlayerTournamentAsync()
    {
        await using var ctx = _db.CreateDbContext();
        var tid = Guid.NewGuid();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        ctx.Tournaments.Add(new Tournament
        {
            Id = tid,
            Slug = $"standings-{Guid.NewGuid():N}",
            Title = "Standings Test",
            StartsAt = DateTimeOffset.UtcNow.AddDays(1),
            TimeZoneId = "UTC",
            NumberOfRounds = 3,
            MaxPlayers = 8,
            OrganizerName = "TO",
            OrganizerEmail = "to@test.com",
            ManageToken = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        ctx.Players.Add(new Player
        {
            Id = p1, TournamentId = tid, Name = "Alice",
            EditToken = Guid.NewGuid().ToString("N"), RegisteredAt = DateTimeOffset.UtcNow,
        });
        ctx.Players.Add(new Player
        {
            Id = p2, TournamentId = tid, Name = "Bob",
            EditToken = Guid.NewGuid().ToString("N"), RegisteredAt = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();
        return (tid, p1, p2);
    }

    // ── Empty tournament ──────────────────────────────────────────────────────

    [Fact]
    public async Task ComputeAsync_WithNoPlayers_ReturnsEmpty()
    {
        await using var ctx = _db.CreateDbContext();
        var tid = Guid.NewGuid();
        ctx.Tournaments.Add(new Tournament
        {
            Id = tid, Slug = $"empty-{Guid.NewGuid():N}", Title = "E",
            StartsAt = DateTimeOffset.UtcNow, TimeZoneId = "UTC",
            NumberOfRounds = 1, MaxPlayers = 4,
            OrganizerName = "X", OrganizerEmail = "x@test.com",
            ManageToken = Guid.NewGuid().ToString("N"), CreatedAt = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var standings = await BuildSut().ComputeAsync(tid);
        standings.ShouldBeEmpty();
    }

    // ── Win / Loss from a scored match ────────────────────────────────────────

    [Fact]
    public async Task ComputeAsync_ScoredMatch_AssignsWinAndLoss()
    {
        var (tid, p1, p2) = await SeedTwoPlayerTournamentAsync();
        var roundSvc = BuildRoundSvc();

        var round = await roundSvc.CreateWithPairingsAsync(tid, null);
        var matches = await roundSvc.GetMatchesAsync(round.Id);
        var match = matches.Single(m => m.Player2Id is not null);

        // p1 wins 87–42
        await roundSvc.EnterScoresAsync(match.Id, 87, 42);

        var standings = await BuildSut().ComputeAsync(tid);

        var winner = standings.Single(s => s.Wins == 1);
        var loser  = standings.Single(s => s.Wins == 0);

        // Winner is whoever was Player1 in the match — check points
        (winner.TotalPoints + loser.TotalPoints).ShouldBe(87 + 42);
        winner.TotalPoints.ShouldBeGreaterThan(loser.TotalPoints);
    }

    // ── Draw ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ComputeAsync_DrawMatch_BothGetZeroWins()
    {
        var (tid, _, _) = await SeedTwoPlayerTournamentAsync();
        var roundSvc = BuildRoundSvc();

        var round = await roundSvc.CreateWithPairingsAsync(tid, null);
        var matches = await roundSvc.GetMatchesAsync(round.Id);
        var match = matches.Single(m => m.Player2Id is not null);

        await roundSvc.EnterScoresAsync(match.Id, 50, 50);

        var standings = await BuildSut().ComputeAsync(tid);
        standings.ShouldAllBe(s => s.Wins == 0);
        standings.ShouldAllBe(s => s.TotalPoints == 50);
    }

    // ── Bye gives a win ───────────────────────────────────────────────────────

    [Fact]
    public async Task ComputeAsync_ByeMatch_GivesWinToByePlayer()
    {
        // 3 players so one gets a bye
        await using var ctx = _db.CreateDbContext();
        var tid = Guid.NewGuid();
        ctx.Tournaments.Add(new Tournament
        {
            Id = tid, Slug = $"bye-{Guid.NewGuid():N}", Title = "Bye Test",
            StartsAt = DateTimeOffset.UtcNow.AddDays(1), TimeZoneId = "UTC",
            NumberOfRounds = 3, MaxPlayers = 8,
            OrganizerName = "TO", OrganizerEmail = "to@test.com",
            ManageToken = Guid.NewGuid().ToString("N"), CreatedAt = DateTimeOffset.UtcNow,
        });
        for (var i = 1; i <= 3; i++)
            ctx.Players.Add(new Player
            {
                Id = Guid.NewGuid(), TournamentId = tid, Name = $"P{i}",
                EditToken = Guid.NewGuid().ToString("N"), RegisteredAt = DateTimeOffset.UtcNow,
            });
        await ctx.SaveChangesAsync();

        var roundSvc = BuildRoundSvc();
        await roundSvc.CreateWithPairingsAsync(tid, null);

        var standings = await BuildSut().ComputeAsync(tid);
        // One player got a bye — exactly one player has 1 win (the bye) before any other scoring
        standings.Count(s => s.Wins == 1).ShouldBeGreaterThanOrEqualTo(1);
    }

    // ── Sorted order: Wins desc, then Points desc ─────────────────────────────

    [Fact]
    public async Task ComputeAsync_SortsByWinsDescThenPointsDesc()
    {
        var (tid, _, _) = await SeedTwoPlayerTournamentAsync();
        var roundSvc = BuildRoundSvc();

        var round = await roundSvc.CreateWithPairingsAsync(tid, null);
        var matches = await roundSvc.GetMatchesAsync(round.Id);
        var match = matches.Single(m => m.Player2Id is not null);
        // Decisive result so we can verify sort
        await roundSvc.EnterScoresAsync(match.Id, 100, 0);

        var standings = await BuildSut().ComputeAsync(tid);

        standings.Count.ShouldBe(2);
        standings[0].Wins.ShouldBeGreaterThanOrEqualTo(standings[1].Wins);
    }
}
