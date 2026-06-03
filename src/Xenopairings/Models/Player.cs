namespace Xenopairings.Models;

public class Player
{
    public Guid Id { get; set; }
    public Guid TournamentId { get; set; }
    public Tournament Tournament { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? ArmyFaction { get; set; }
    public string? ArmyList { get; set; }  // free text, can be long
    public string EditToken { get; set; } = string.Empty;
    public DateTimeOffset RegisteredAt { get; set; }
    public bool IsDropped { get; set; }  // withdrawn from tournament mid-event
    public Guid? TeamId { get; set; }
    public Team? Team { get; set; }
    /// <summary>The organization the player chose to represent in this tournament. Null = none.</summary>
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    /// <summary>True when the player has checked in for the event. Only relevant when Tournament.CheckInEnabled is true.</summary>
    public bool IsCheckedIn { get; set; }
}
