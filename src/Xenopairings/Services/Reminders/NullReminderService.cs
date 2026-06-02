namespace Xenopairings.Services.Reminders;

/// <summary>
/// No-op implementation of <see cref="IReminderService"/> used when
/// <c>Reminders:Enabled = false</c> or as the default stub until reminder
/// functionality is implemented.
/// </summary>
public sealed class NullReminderService : IReminderService
{
    public Task NoOpAsync() => Task.CompletedTask;
}
