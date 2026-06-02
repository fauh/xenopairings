namespace Xenopairings.Services.Pairings;

/// <summary>
/// Pure-static WTC (World Team Championship) scoring converter.
///
/// Converts raw battle-point scores into a 0–20 game-point split using the
/// standard WTC 40K differential table. The two game-point values always sum to 20.
///
/// Table:
///   diff 0–2  → 10 / 10   (draw)
///   diff 3–5  → 11 /  9
///   diff 6–9  → 12 /  8
///   diff 10–14 → 13 /  7
///   diff 15–20 → 14 /  6
///   diff 21–27 → 15 /  5
///   diff 28–35 → 16 /  4
///   diff 36–44 → 17 /  3
///   diff 45–54 → 18 /  2
///   diff 55+   → 20 /  0
/// </summary>
public static class WtcScoring
{
    /// <summary>
    /// Converts raw battle-point scores to game points.
    /// Returns (p1GamePoints, p2GamePoints). Both values are in [0, 20] and sum to 20.
    /// </summary>
    public static (int p1Gp, int p2Gp) ConvertToGamePoints(int p1Raw, int p2Raw)
    {
        var diff = Math.Abs(p1Raw - p2Raw);

        var winnerGp = diff switch
        {
            <= 2  => 10,
            <= 5  => 11,
            <= 9  => 12,
            <= 14 => 13,
            <= 20 => 14,
            <= 27 => 15,
            <= 35 => 16,
            <= 44 => 17,
            <= 54 => 18,
            _     => 20,
        };

        var loserGp = 20 - winnerGp;

        // Assign winner GP to whichever player scored more raw BPs.
        // When p1Raw == p2Raw the diff is 0, winnerGp == loserGp == 10 — order doesn't matter.
        return p1Raw >= p2Raw ? (winnerGp, loserGp) : (loserGp, winnerGp);
    }
}
