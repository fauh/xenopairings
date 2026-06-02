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
    public bool IsClosed { get; set; }
    public string OrganizerName { get; set; } = string.Empty;
    public string OrganizerEmail { get; set; } = string.Empty;
    public string ManageToken { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public bool RegistrationOpen { get; set; } = true;
}
