namespace Xenopairings.Models;

public enum TiebreakerType
{
    /// <summary>Total battle/game points scored across all rounds.</summary>
    Points,
    /// <summary>Average total points of all opponents faced (Strength of Schedule).</summary>
    StrengthOfSchedule,
    /// <summary>Average SOS of all opponents faced (Extended SOS).</summary>
    ExtendedStrengthOfSchedule,
    /// <summary>Direct match result. Only resolves 2-way ties where the two players faced each other; skipped otherwise.</summary>
    HeadToHead,
    /// <summary>Random fallback — consistent within a computation run, guarantees a total order.</summary>
    Random,
}
