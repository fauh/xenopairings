using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xenopairings.Models;
using Xenopairings.Services.Elo;
using Xenopairings.Services.Email;
using Xenopairings.Services.Rounds;
using Xenopairings.Services.Standings;
using Xenopairings.Tests.Infrastructure;
using Shouldly;

namespace Xenopairings.Tests.Services;

public class RoundServiceTests : IClassFixture<InMemoryDatabaseFixture>
{
    private readonly InMemoryDatabaseFixture _db;

    public RoundServiceTests(InMemoryDatabaseFixture db) => _db = db;

    private RoundService BuildSut()
    {
        var ctx = _db.CreateDbContext();
        var standings = new StandingsService(ctx);
        return new RoundService(
            ctx, standings, new TeamStandingsService(ctx),
            new NullEmailSender(),
            Options.Create(new EmailSettings { BaseUrl = "https://test.example" }),
            NullLogger<RoundService>.Instance);
    }

    private async Task<(Guid tournamentId, List<Guid> playerIds)> SeedTournamentAsync(int playerCount)
    {
        await using var ctx = _db.CreateDbContext();
        var tournamentId = Guid.NewGuid();
        var tournament = new Tournament
        {
            Id = tournamentId,
            Slug = $"round-test-{Guid.NewGuid():N}",
            Title = "Round Test Tournament",
            StartsAt = DateTimeOffset.UtcNow.AddDays(7),
            TimeZoneId = "UTC",
            NumberOfRounds = 5,
            MaxPlayers = 16,
            OrganizerName = "TO",
            OrganizerEmail = "to@test.com",
            ManageToken = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Tournaments.Add(tournament);

        var playerIds = new List<Guid>();
        for (var i = 1; i <= playerCount; i++)
        {
            var pid = Guid.NewGuid();
            playerIds.Add(pid);
            ctx.Players.Add(new Player
            {
                Id = pid,
                TournamentId = tournamentId,
                Name = $"Player {i}",
                EditToken = Guid.NewGuid().ToString("N"),
                RegisteredAt = DateTimeOffset.UtcNow,
            });
        }
        await ctx.SaveChangesAsync();
        return (tournamentId, playerIds);
    }

    // ── CreateWithPairingsAsync ───────────────────────────────────────────────

    [Fact]
    public async Task CreateWithPairingsAsync_WithFourPlayers_CreatesTwoMatches()
    {
        var (tid, _) = await SeedTournamentAsync(4);
        var sut = BuildSut();

        var round = await sut.CreateWithPairingsAsync(tid, "Priority Target");

        round.RoundNumber.ShouldBe(1);
        round.MissionLayout.ShouldBe("Priority Target");

        var matches = await sut.GetMatchesAsync(round.Id);
        matches.Count.ShouldBe(2);
        matches.ShouldAllBe(m => m.Player1Id.HasValue);
        matches.ShouldAllBe(m => m.Player2Id.HasValue);  // no byes with 4 players
    }

    [Fact]
    public async Task CreateWithPairingsAsync_WithOddPlayers_CreatesByeMatch()
    {
        var (tid, _) = await SeedTournamentAsync(3);
        var sut = BuildSut();

        var round = await sut.CreateWithPairingsAsync(tid, null);

        var matches = await sut.GetMatchesAsync(round.Id);
        matches.Count.ShouldBe(2);  // 1 normal + 1 bye
        matches.Count(m => m.Player2Id is null).ShouldBe(1);
    }

    [Fact]
    public async Task CreateWithPairingsAsync_ByeMatchIsAutoScored()
    {
        var (tid, _) = await SeedTournamentAsync(3);
        var sut = BuildSut();

        var round = await sut.CreateWithPairingsAsync(tid, null);
        var matches = await sut.GetMatchesAsync(round.Id);

        var bye = matches.Single(m => m.Player2Id is null);
        bye.IsScored.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateWithPairingsAsync_ThrowsWhenPreviousRoundIncomplete()
    {
        var (tid, _) = await SeedTournamentAsync(4);
        var sut = BuildSut();

        // Create round 1 but don't complete it
        await sut.CreateWithPairingsAsync(tid, null);

        // Attempt round 2 — should throw
        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.CreateWithPairingsAsync(tid, null));
    }

    [Fact]
    public async Task CreateWithPairingsAsync_ThrowsWithNoActivePlayers()
    {
        await using var ctx = _db.CreateDbContext();
        var tid = Guid.NewGuid();
        ctx.Tournaments.Add(new Tournament
        {
            Id = tid,
            Slug = $"empty-{Guid.NewGuid():N}",
            Title = "Empty",
            StartsAt = DateTimeOffset.UtcNow.AddDays(1),
            TimeZoneId = "UTC",
            NumberOfRounds = 3,
            MaxPlayers = 8,
            OrganizerName = "TO",
            OrganizerEmail = "to@test.com",
            ManageToken = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();

        await Should.ThrowAsync<InvalidOperationException>(
            () => BuildSut().CreateWithPairingsAsync(tid, null));
    }

    // ── EnterScoresAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task EnterScoresAsync_SetsIsScored()
    {
        var (tid, _) = await SeedTournamentAsync(2);
        var sut = BuildSut();

        var round = await sut.CreateWithPairingsAsync(tid, null);
        var matches = await sut.GetMatchesAsync(round.Id);
        var match = matches.Single(m => m.Player2Id is not null);

        await sut.EnterScoresAsync(match.Id, 87, 42);

        var refreshed = (await sut.GetMatchesAsync(round.Id))
            .Single(m => m.Id == match.Id);
        refreshed.IsScored.ShouldBeTrue();
        refreshed.Player1Score.ShouldBe(87);
        refreshed.Player2Score.ShouldBe(42);
    }

    [Fact]
    public async Task EnterScoresAsync_NegativeScore_Throws()
    {
        var (tid, _) = await SeedTournamentAsync(2);
        var sut = BuildSut();
        var round = await sut.CreateWithPairingsAsync(tid, null);
        var matches = await sut.GetMatchesAsync(round.Id);
        var match = matches.Single(m => m.Player2Id is not null);

        await Should.ThrowAsync<ArgumentException>(
            () => sut.EnterScoresAsync(match.Id, -1, 50));
    }

    // ── CompleteRoundAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task CompleteRoundAsync_WhenAllScored_SetsIsComplete()
    {
        var (tid, _) = await SeedTournamentAsync(2);
        var sut = BuildSut();

        var round = await sut.CreateWithPairingsAsync(tid, null);
        var matches = await sut.GetMatchesAsync(round.Id);
        var regularMatch = matches.SingleOrDefault(m => m.Player2Id is not null);
        if (regularMatch is not null)
            await sut.EnterScoresAsync(regularMatch.Id, 75, 55);

        await sut.CompleteRoundAsync(round.Id);

        var refreshed = await sut.GetAsync(round.Id);
        refreshed!.IsComplete.ShouldBeTrue();
    }

    [Fact]
    public async Task CompleteRoundAsync_WithUnscoredMatches_Throws()
    {
        var (tid, _) = await SeedTournamentAsync(4);
        var sut = BuildSut();

        var round = await sut.CreateWithPairingsAsync(tid, null);
        // Don't enter scores — should throw

        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.CompleteRoundAsync(round.Id));
    }
}
