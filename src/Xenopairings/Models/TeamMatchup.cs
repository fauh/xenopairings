namespace Xenopairings.Models;

public class TeamMatchup
{
    public Guid Id { get; set; }
    public Guid RoundId { get; set; }
    public Round Round { get; set; } = null!;
    public Guid Team1Id { get; set; }
    public Team Team1 { get; set; } = null!;
    /// <summary>Null when Team1 has a bye.</summary>
    public Guid? Team2Id { get; set; }
    public Team? Team2 { get; set; }
    /// <summary>
    /// The first table number assigned to this matchup.
    /// e.g. if TeamSize=3 and this is the second matchup, TableGroupStart = 4 (tables 4–6).
    /// </summary>
    public int TableGroupStart { get; set; }
}
