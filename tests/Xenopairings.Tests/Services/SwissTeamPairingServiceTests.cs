using Xenopairings.Models;
using Xenopairings.Services.Pairings;
using Shouldly;

namespace Xenopairings.Tests.Services;

public class SwissTeamPairingServiceTests
{
    private static Team MakeTeam(string name) => new()
    {
        Id = Guid.NewGuid(), TournamentId = Guid.NewGuid(),
        Name = name, InviteToken = Guid.NewGuid().ToString("N"),
        CreatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public void FourTeams_CreatesTwoPairings()
    {
        var teams = new[] { MakeTeam("A"), MakeTeam("B"), MakeTeam("C"), MakeTeam("D") };
        var result = SwissTeamPairingService.Generate(teams, [], [], teamSize: 3);
        result.Count.ShouldBe(2);
        result.ShouldAllBe(p => p.t2 != null);
    }

    [Fact]
    public void ThreeTeams_OneByePairing()
    {
        var teams = new[] { MakeTeam("A"), MakeTeam("B"), MakeTeam("C") };
        var result = SwissTeamPairingService.Generate(teams, [], [], teamSize: 3);
        result.Count.ShouldBe(2);
        result.Count(p => p.t2 == null).ShouldBe(1);
    }

    [Fact]
    public void EmptyTeams_ReturnsEmpty()
    {
        var result = SwissTeamPairingService.Generate([], [], [], teamSize: 3);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void TableGroupStart_IncrementsByTeamSize()
    {
        var teams = new[] { MakeTeam("A"), MakeTeam("B"), MakeTeam("C"), MakeTeam("D") };
        var result = SwissTeamPairingService.Generate(teams, [], [], teamSize: 3);
        var starts = result.Select(p => p.tableGroupStart).OrderBy(x => x).ToList();
        starts[0].ShouldBe(1);
        starts[1].ShouldBe(4);  // 1 + teamSize(3)
    }

    [Fact]
    public void AvoidsRepeatMatchupsWhenPossible()
    {
        var a = MakeTeam("A");
        var b = MakeTeam("B");
        var c = MakeTeam("C");
        var d = MakeTeam("D");

        // Previous round: A vs B, C vs D
        var previous = new List<(Guid, Guid)> { (a.Id, b.Id), (c.Id, d.Id) };
        var standings = new List<TeamStanding>
        {
            new(a.Id, 1, 0, 30), new(c.Id, 1, 0, 28),
            new(b.Id, 0, 0, 10), new(d.Id, 0, 0, 8),
        };

        var result = SwissTeamPairingService.Generate(
            new[] { a, b, c, d }, standings, previous, teamSize: 3);

        result.Count.ShouldBe(2);
        // Neither pairing should repeat A-B or C-D
        var pairSet = result.Select(p =>
            new HashSet<Guid> { p.t1.Id, p.t2!.Id }).ToList();
        pairSet.Any(s => s.SetEquals(new[] { a.Id, b.Id })).ShouldBeFalse();
        pairSet.Any(s => s.SetEquals(new[] { c.Id, d.Id })).ShouldBeFalse();
    }

}
