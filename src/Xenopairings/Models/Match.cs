namespace Xenopairings.Models;

public class Match
{
    public Guid Id { get; set; }
    public Guid RoundId { get; set; }
    public Round Round { get; set; } = null!;
    public int TableNumber { get; set; }
    public Guid? Player1Id { get; set; }
    public Player? Player1 { get; set; }
    public Guid? Player2Id { get; set; }
    public Player? Player2 { get; set; }
    // null = not yet entered, int = battle points scored
    public int? Player1Score { get; set; }
    public int? Player2Score { get; set; }
    // true when both scores are entered
    public bool IsScored { get; set; }
    /// <summary>True = Player1 was the attacker (deployed second). Null = not yet recorded.</summary>
    public bool? Player1IsAttacker { get; set; }
    /// <summary>True = Player1 had the first turn. Null = not yet recorded.</summary>
    public bool? Player1WentFirst { get; set; }
    /// <summary>Null for individual-event matches. Set for matches inside a team matchup.</summary>
    public Guid? TeamMatchupId { get; set; }
    public TeamMatchup? TeamMatchup { get; set; }
    /// <summary>1–5 sportsmanship rating given by Player 1 for this game. Null = not yet rated.</summary>
    public int? Player1SportsRating { get; set; }
    /// <summary>1–5 sportsmanship rating given by Player 2 for this game. Null = not yet rated.</summary>
    public int? Player2SportsRating { get; set; }

    // ── Score breakdown ───────────────────────────────────────────────────────
    /// <summary>Primary objective score (0–45). Null when not yet entered or tournament uses single-total entry.</summary>
    public int? Player1PrimaryScore { get; set; }
    public int? Player1SecondaryScore { get; set; }
    /// <summary>Battle Ready painting bonus (+10 if true). Default true (assumed painted).</summary>
    public bool Player1BattleReady { get; set; } = true;

    public int? Player2PrimaryScore { get; set; }
    public int? Player2SecondaryScore { get; set; }
    public bool Player2BattleReady { get; set; } = true;
}
