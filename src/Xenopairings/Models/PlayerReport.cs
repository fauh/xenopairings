namespace Xenopairings.Models;

public class PlayerReport
{
    public Guid Id { get; set; }
    public Guid TournamentId { get; set; }
    public Tournament Tournament { get; set; } = null!;
    /// <summary>Null if reported anonymously or reporter is not a registered player.</summary>
    public Guid? ReporterPlayerId { get; set; }
    public Player? ReporterPlayer { get; set; }
    public Guid ReportedPlayerId { get; set; }
    public Player ReportedPlayer { get; set; } = null!;
    /// <summary>Free-text reason for the report (max 500 chars).</summary>
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset FiledAt { get; set; }
    public bool IsResolved { get; set; }
    public string? OrganizerNote { get; set; }
}
