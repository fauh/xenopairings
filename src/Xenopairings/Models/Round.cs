namespace Xenopairings.Models;

public class Round
{
    public Guid Id { get; set; }
    public Guid TournamentId { get; set; }
    public Tournament Tournament { get; set; } = null!;
    public int RoundNumber { get; set; }
    public string? MissionLayout { get; set; }  // text description of the mission
    public bool IsComplete { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
