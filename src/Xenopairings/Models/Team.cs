namespace Xenopairings.Models;

public class Team
{
    public Guid Id { get; set; }
    public Guid TournamentId { get; set; }
    public Tournament Tournament { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    /// <summary>Player who created this team. Null if the team was created by the organizer.</summary>
    public Guid? CaptainPlayerId { get; set; }
    /// <summary>URL-safe token shared with teammates to join the team.</summary>
    public string InviteToken { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation: players on this team
    public ICollection<Player> Players { get; set; } = [];
}
