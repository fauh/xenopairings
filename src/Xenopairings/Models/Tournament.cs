using System.Text.Json;
using System.Text.Json.Serialization;

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

    // ── Phase 3b additions ────────────────────────────────────────────────────

    /// <summary>
    /// JSON-serialised ordered list of TiebreakerType values applied after Wins.
    /// Default: Points → StrengthOfSchedule → Random.
    /// </summary>
    public string TiebreakersJson { get; set; } =
        "[\"Points\",\"StrengthOfSchedule\",\"Random\"]";

    // Options shared for both serialize and deserialize to keep stored values as strings.
    private static readonly JsonSerializerOptions TbJsonOpts = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly IReadOnlyList<TiebreakerType> DefaultTiebreakers =
        [TiebreakerType.Points, TiebreakerType.StrengthOfSchedule, TiebreakerType.Random];

    /// <summary>Convenience accessor — deserialises TiebreakersJson. Falls back to defaults on empty/invalid JSON.</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public IReadOnlyList<TiebreakerType> Tiebreakers
    {
        get
        {
            if (string.IsNullOrWhiteSpace(TiebreakersJson)) return DefaultTiebreakers;
            try { return JsonSerializer.Deserialize<List<TiebreakerType>>(TiebreakersJson, TbJsonOpts) ?? DefaultTiebreakers; }
            catch { return DefaultTiebreakers; }
        }
    }

    internal static string SerializeTiebreakers(IEnumerable<TiebreakerType> tbs) =>
        JsonSerializer.Serialize(tbs.ToList(), TbJsonOpts);

    /// <summary>Top-cut size after Swiss (4/8/16/32). Null = no cut.</summary>
    public int? TopCutSize { get; set; }

    /// <summary>When true, only checked-in players are included in round-1 pairings.</summary>
    public bool CheckInEnabled { get; set; }
    /// <summary>When true, the tournament is hidden from the public browse page and excluded from ELO calculations.</summary>
    public bool IsTestEvent { get; set; }
}
