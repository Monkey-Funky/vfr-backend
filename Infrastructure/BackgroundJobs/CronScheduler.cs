namespace Infrastructure.BackgroundJobs;

/// <summary>
/// Lightweight scheduler helper. Computes the delay to the next UTC wall-clock
/// occurrence and awaits it. Designed for simple daily-at-HH:MM job schedules.
///
/// All five VFR background jobs use this helper instead of a full cron library.
/// See 09-BackgroundJobs.md §7 for the canonical design rationale.
/// </summary>
public static class CronScheduler
{
    /// <summary>
    /// Waits asynchronously until the next occurrence of <paramref name="utcTime"/>
    /// (formatted as "HH:mm") in UTC. If the specified time has already passed today,
    /// waits until the same time tomorrow.
    /// </summary>
    /// <param name="utcTime">Target wall-clock time in "HH:mm" format (UTC).</param>
    /// <param name="cancellationToken">Propagates host shutdown signal.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="utcTime"/> cannot be parsed as "HH:mm".
    /// </exception>
    public static async Task WaitForNextOccurrenceAsync(
        string utcTime,
        CancellationToken cancellationToken)
    {
        if (!TimeOnly.TryParse(utcTime, out TimeOnly scheduledTime))
            throw new ArgumentException(
                $"Invalid time format '{utcTime}'. Expected 'HH:mm'.", nameof(utcTime));

        DateTime utcNow = DateTime.UtcNow;

        // Compute next occurrence today at the specified wall-clock time.
        DateTime nextRun = DateTime.UtcNow.Date
            .AddHours(scheduledTime.Hour)
            .AddMinutes(scheduledTime.Minute);

        // If that moment has already passed today, roll forward to tomorrow.
        if (nextRun <= utcNow)
            nextRun = nextRun.AddDays(1);

        TimeSpan delay = nextRun - utcNow;

        await Task.Delay(delay, cancellationToken);
    }
}