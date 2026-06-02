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
    /// <summary>Null for individual-event matches. Set for matches inside a team matchup.</summary>
    public Guid? TeamMatchupId { get; set; }
    public TeamMatchup? TeamMatchup { get; set; }
}
