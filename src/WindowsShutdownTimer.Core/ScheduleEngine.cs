namespace WindowsShutdownTimer.Core;

public static class ScheduleEngine
{
    public static readonly TimeSpan DefaultMaxLateness = TimeSpan.FromMinutes(2);

    public static DateTime GetOccurrence(DateTime date, string timeOfDay)
    {
        var time = TimeOfDayParser.Parse(timeOfDay);
        return date.Date + time;
    }

    public static DateTime GetNextOccurrence(DateTime now, string timeOfDay)
    {
        var occurrence = GetOccurrence(now, timeOfDay);
        return occurrence > now ? occurrence : occurrence.AddDays(1);
    }

    public static IReadOnlyList<DueScheduleEvent> GetDueEvents(
        DateTime now,
        AppSettings settings,
        ScheduleState state,
        TimeSpan maxLateness)
    {
        state.Trim(now);
        settings.Normalize();

        if (!settings.Enabled)
        {
            return [];
        }

        var due = new List<DueScheduleEvent>();

        foreach (var reminder in settings.Reminders.Where(r => r.Enabled))
        {
            var scheduledAt = GetOccurrence(now, reminder.Time);
            var date = DateOnly.FromDateTime(scheduledAt);
            var key = $"reminder:{reminder.Id}:{date:yyyy-MM-dd}";

            if (IsDue(now, scheduledAt, maxLateness) && !state.HasFired(key))
            {
                state.MarkFired(key);
                due.Add(new DueScheduleEvent(ScheduleEventType.Reminder, scheduledAt, reminder));
            }
        }

        if (settings.AutoShutdown)
        {
            var scheduledAt = GetOccurrence(now, settings.ShutdownTime);
            var date = DateOnly.FromDateTime(scheduledAt);
            var key = $"shutdown:{date:yyyy-MM-dd}";

            if (IsDue(now, scheduledAt, maxLateness) &&
                !state.HasFired(key) &&
                !state.IsShutdownPaused(date))
            {
                state.MarkFired(key);
                due.Add(new DueScheduleEvent(ScheduleEventType.Shutdown, scheduledAt, null));
            }
        }

        return due;
    }

    private static bool IsDue(DateTime now, DateTime scheduledAt, TimeSpan maxLateness)
    {
        var lateBy = now - scheduledAt;
        return lateBy >= TimeSpan.Zero && lateBy <= maxLateness;
    }
}
