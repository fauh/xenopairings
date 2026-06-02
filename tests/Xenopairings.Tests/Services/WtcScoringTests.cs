using Xenopairings.Services.Pairings;
using Shouldly;

namespace Xenopairings.Tests.Services;

public class WtcScoringTests
{
    // ── Draw (diff 0–2) ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(50, 50)]   // exact tie
    [InlineData(51, 50)]   // diff 1
    [InlineData(52, 50)]   // diff 2
    public void Diff0To2_Returns10_10(int p1, int p2)
    {
        var (gp1, gp2) = WtcScoring.ConvertToGamePoints(p1, p2);
        gp1.ShouldBe(10);
        gp2.ShouldBe(10);
    }

    // ── 11/9 band (diff 3–5) ─────────────────────────────────────────────────

    [Theory]
    [InlineData(53, 50)]  // diff 3
    [InlineData(55, 50)]  // diff 5
    public void Diff3To5_Returns11_9(int p1, int p2)
    {
        var (gp1, gp2) = WtcScoring.ConvertToGamePoints(p1, p2);
        gp1.ShouldBe(11);
        gp2.ShouldBe(9);
    }

    // ── 12/8 band (diff 6–9) ─────────────────────────────────────────────────

    [Theory]
    [InlineData(56, 50)]  // diff 6
    [InlineData(59, 50)]  // diff 9
    public void Diff6To9_Returns12_8(int p1, int p2)
    {
        var (gp1, gp2) = WtcScoring.ConvertToGamePoints(p1, p2);
        gp1.ShouldBe(12);
        gp2.ShouldBe(8);
    }

    // ── Maximum (diff 55+) ────────────────────────────────────────────────────

    [Theory]
    [InlineData(100, 44)]  // diff 56
    [InlineData(100, 0)]   // diff 100
    public void Diff55Plus_Returns20_0(int p1, int p2)
    {
        var (gp1, gp2) = WtcScoring.ConvertToGamePoints(p1, p2);
        gp1.ShouldBe(20);
        gp2.ShouldBe(0);
    }

    // ── Direction: loser gets the lower GP ───────────────────────────────────

    [Fact]
    public void HigherRawScoreGetsHigherGP()
    {
        var (gp1, gp2) = WtcScoring.ConvertToGamePoints(70, 40);  // diff 30 → 16/4
        gp1.ShouldBe(16);
        gp2.ShouldBe(4);
    }

    [Fact]
    public void LowerRawScoreGetsLowerGP()
    {
        var (gp1, gp2) = WtcScoring.ConvertToGamePoints(40, 70);  // diff 30 → p2 wins
        gp1.ShouldBe(4);
        gp2.ShouldBe(16);
    }

    // ── Always sums to 20 ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 0)]
    [InlineData(75, 45)]
    [InlineData(50, 48)]
    public void AlwaysSumsTo20(int p1, int p2)
    {
        var (gp1, gp2) = WtcScoring.ConvertToGamePoints(p1, p2);
        (gp1 + gp2).ShouldBe(20);
    }

    // ── Band boundaries ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(13, 0,  13, 7)]   // diff 13 → band 10-14
    [InlineData(14, 0,  13, 7)]   // diff 14 → band 10-14
    [InlineData(15, 0,  14, 6)]   // diff 15 → band 15-20
    [InlineData(20, 0,  14, 6)]   // diff 20 → band 15-20
    [InlineData(21, 0,  15, 5)]   // diff 21 → band 21-27
    [InlineData(54, 0,  18, 2)]   // diff 54 → band 45-54
    [InlineData(55, 0,  20, 0)]   // diff 55 → max
    public void BandBoundaries(int p1, int p2, int expectedGp1, int expectedGp2)
    {
        var (gp1, gp2) = WtcScoring.ConvertToGamePoints(p1, p2);
        gp1.ShouldBe(expectedGp1);
        gp2.ShouldBe(expectedGp2);
    }
}
