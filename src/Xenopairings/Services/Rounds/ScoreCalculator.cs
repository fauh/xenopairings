namespace Xenopairings.Services.Rounds;

/// <summary>
/// Helpers for the standard competitive 40K scoring format:
///   Total = Primary (0–45) + Secondary (0–45) + Battle Ready (0 or 10) = 0–100
/// </summary>
public static class ScoreCalculator
{
    public const int BattleReadyBonus  = 10;
    public const int PrimaryMax        = 45;
    public const int SecondaryMax      = 45;
    public const int TotalMax          = PrimaryMax + SecondaryMax + BattleReadyBonus; // 100

    public static int ComputeTotal(int primary, int secondary, bool battleReady)
        => primary + secondary + (battleReady ? BattleReadyBonus : 0);

    /// <summary>Returns true when the breakdown values are valid for submission.</summary>
    public static bool IsValid(int primary, int secondary)
        => primary is >= 0 and <= PrimaryMax && secondary is >= 0 and <= SecondaryMax;
}
