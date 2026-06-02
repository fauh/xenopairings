using Xenopairings.Models;
using Xenopairings.Services.Pairings;
using Shouldly;

namespace Xenopairings.Tests.Services;

public class SwissPairingServiceTests
{
    private static Player MakePlayer(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        EditToken = Guid.NewGuid().ToString(),
        RegisteredAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public void Generate_EmptyPlayers_ReturnsEmpty()
    {
        var result = SwissPairingService.Generate([], [], []);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Generate_EvenPlayers_ProducesCorrectPairingCount()
    {
        var players = Enumerable.Range(1, 8).Select(i => MakePlayer($"P{i}")).ToList();
        var result = SwissPairingService.Generate(players, [], []);
        result.Count.ShouldBe(4);
    }

    [Fact]
    public void Generate_OddPlayers_ProducesByeForLastPlayer()
    {
        var players = Enumerable.Range(1, 5).Select(i => MakePlayer($"P{i}")).ToList();
        var result = SwissPairingService.Generate(players, [], []);
        result.Count.ShouldBe(3);
        result.Count(r => r.p2 is null).ShouldBe(1);
    }

    [Fact]
    public void Generate_TableNumbersAreSequential()
    {
        var players = Enumerable.Range(1, 6).Select(i => MakePlayer($"P{i}")).ToList();
        var result = SwissPairingService.Generate(players, [], []);
        result.Select(r => r.table).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public void Generate_DroppedPlayers_AreExcluded()
    {
        var players = Enumerable.Range(1, 4).Select(i => MakePlayer($"P{i}")).ToList();
        players[0].IsDropped = true;
        var result = SwissPairingService.Generate(players, [], []);
        // 3 active players → 1 match + 1 bye
        result.Count.ShouldBe(2);
    }

    [Fact]
    public void Generate_AllPlayersAppearExactlyOnce()
    {
        var players = Enumerable.Range(1, 8).Select(i => MakePlayer($"P{i}")).ToList();
        var result = SwissPairingService.Generate(players, [], []);

        var paired = result.SelectMany(r => new[] { r.p1, r.p2 })
            .Where(p => p is not null)
            .Select(p => p!.Id)
            .ToList();

        paired.Distinct().Count().ShouldBe(8);
        paired.Count.ShouldBe(8);
    }
}
