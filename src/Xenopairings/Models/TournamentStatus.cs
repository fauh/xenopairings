namespace Xenopairings.Models;

public enum TournamentStatus
{
    /// <summary>Default. Registration may be open or closed. No rounds have started.</summary>
    Upcoming = 0,
    /// <summary>Organizer has started the event. Registration is locked. Rounds can be played.</summary>
    InProgress = 1,
    /// <summary>Organizer has ended the event. No further changes allowed.</summary>
    Ended = 2,
}
