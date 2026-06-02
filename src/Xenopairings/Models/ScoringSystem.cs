namespace Xenopairings.Models;

public enum ScoringSystem
{
    /// <summary>
    /// GW-style: raw battle points entered directly. Win = more BPs, draw = equal BPs.
    /// Swiss standings use raw battle points.
    /// </summary>
    Gw = 0,

    /// <summary>
    /// WTC-style: raw battle points converted to a 0–20 game-point split via the
    /// standard differential table. Win = GP &gt; 10, draw = GP == 10.
    /// Swiss standings use converted game points.
    /// </summary>
    Wtc = 1,
}
