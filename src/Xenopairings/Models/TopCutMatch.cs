namespace Xenopairings.Models;

public class TopCutMatch
{
    public Guid Id { get; set; }
    public Guid TournamentId { get; set; }
    public Tournament Tournament { get; set; } = null!;
    /// <summary>1 = first elimination round (QF for top 8), 2 = SF, 3 = Final, etc.</summary>
    public int BracketRound { get; set; }
    /// <summary>1-indexed match number within this BracketRound.</summary>
    public int MatchNumber { get; set; }
    public int Seed1 { get; set; }
    /// <summary>0 = bye.</summary>
    public int Seed2 { get; set; }
    public Guid? Player1Id { get; set; }
    public Player? Player1 { get; set; }
    public Guid? Player2Id { get; set; }
    public Player? Player2 { get; set; }
    public int? Player1Score { get; set; }
    public int? Player2Score { get; set; }
    public bool IsScored { get; set; }
    public Guid? WinnerId { get; set; }
}
