namespace Xenopairings.Services.Notifications;

/// <summary>
/// In-process pub/sub for tournament events.
/// Registered as a singleton so all Blazor Server circuits (one per connected
/// browser tab) share the same instance and can receive each other's events.
/// </summary>
public sealed class TournamentNotificationService
{
    /// <summary>
    /// Fired when a new round is started for a tournament.
    /// Handlers receive the tournament ID and the new round number.
    /// </summary>
    public event Func<Guid, int, Task>? RoundStarted;

    /// <summary>
    /// Call from ManageTournament after a round is successfully created.
    /// Notifies every subscriber (i.e. every browser tab currently viewing
    /// any tournament page) so they can refresh if it's their tournament.
    /// </summary>
    public async Task NotifyRoundStartedAsync(Guid tournamentId, int roundNumber)
    {
        var handler = RoundStarted;
        if (handler is null) return;

        // Invoke all subscribers concurrently; swallow individual failures so
        // a broken circuit doesn't block the others.
        var tasks = handler.GetInvocationList()
            .Cast<Func<Guid, int, Task>>()
            .Select(h => InvokeSafe(h, tournamentId, roundNumber));
        await Task.WhenAll(tasks);
    }

    private static async Task InvokeSafe(Func<Guid, int, Task> handler, Guid tournamentId, int roundNumber)
    {
        try { await handler(tournamentId, roundNumber); }
        catch { /* circuit may have disconnected */ }
    }
}
