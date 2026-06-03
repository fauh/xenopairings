namespace Xenopairings.Models;

public class Tournament
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public string TimeZoneId { get; set; } = "UTC";
    public int NumberOfRounds { get; set; }
    public int MaxPlayers { get; set; }
    public bool IsPrivate { get; set; }
    public TournamentStatus Status { get; set; } = TournamentStatus.Upcoming;
    /// <summary>When true, players can no longer edit their army list or faction.</summary>
    public bool ArmyListLocked { get; set; }
    public string OrganizerName { get; set; } = string.Empty;
    public string OrganizerEmail { get; set; } = string.Empty;
    public string ManageToken { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public bool RegistrationOpen { get; set; } = true;
    public ScoringSystem ScoringSystem { get; set; } = ScoringSystem.Gw;
    public bool IsTeamEvent { get; set; }
    /// <summary>Number of players per team. Null for individual events.</summary>
    public int? TeamSize { get; set; }
}
