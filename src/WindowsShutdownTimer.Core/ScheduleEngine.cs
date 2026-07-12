namespace WindowsShutdownTimer.Core;

public static class ScheduleEngine
{
    public static readonly TimeSpan DefaultMaxLateness = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan ShutdownCountdownLeadTime = TimeSpan.FromSeconds(10);

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
            var shutdownDate = GetReminderShutdownDate(scheduledAt, reminder);
            var key = $"reminder:{reminder.Id}:{date:yyyy-MM-dd}";

            if (IsDue(now, scheduledAt, maxLateness) &&
                !state.HasFired(key) &&
                !state.IsShutdownPaused(shutdownDate))
            {
                state.MarkFired(key);
                due.Add(new DueScheduleEvent(ScheduleEventType.Reminder, scheduledAt, reminder));
            }
        }

        if (settings.AutoShutdown)
        {
            var scheduledAt = GetShutdownOccurrence(now, settings.ShutdownTime, maxLateness);
            var date = DateOnly.FromDateTime(scheduledAt);
            var key = $"shutdown:{date:yyyy-MM-dd}";

            if (IsShutdownDue(now, scheduledAt, maxLateness) &&
                !state.HasFired(key) &&
                !state.IsShutdownPaused(date))
            {
                state.MarkFired(key);
                due.Add(new DueScheduleEvent(ScheduleEventType.Shutdown, scheduledAt, null));
            }
        }

        return due;
    }

    public static DateTime GetNextCheckTime(
        DateTime now,
        AppSettings settings,
        ScheduleState state,
        TimeSpan maxLateness,
        TimeSpan correctionInterval)
    {
        state.Trim(now);
        settings.Normalize();

        var nextCheck = now + correctionInterval;
        if (!settings.Enabled)
        {
            return nextCheck;
        }

        foreach (var reminder in settings.Reminders.Where(r => r.Enabled))
        {
            foreach (var scheduledAt in GetCandidateOccurrences(now, reminder.Time, maxLateness))
            {
                var date = DateOnly.FromDateTime(scheduledAt);
                var shutdownDate = GetReminderShutdownDate(scheduledAt, reminder);
                var key = $"reminder:{reminder.Id}:{date:yyyy-MM-dd}";

                if (state.HasFired(key) || state.IsShutdownPaused(shutdownDate))
                {
                    continue;
                }

                nextCheck = Min(nextCheck, scheduledAt <= now ? now : scheduledAt);
                break;
            }
        }

        if (settings.AutoShutdown)
        {
            foreach (var scheduledAt in GetShutdownCandidateOccurrences(now, settings.ShutdownTime, maxLateness))
            {
                var date = DateOnly.FromDateTime(scheduledAt);
                var key = $"shutdown:{date:yyyy-MM-dd}";

                if (state.HasFired(key) || state.IsShutdownPaused(date))
                {
                    continue;
                }

                var countdownStart = scheduledAt - ShutdownCountdownLeadTime;
                nextCheck = Min(nextCheck, countdownStart <= now ? now : countdownStart);
                break;
            }
        }

        return nextCheck;
    }

    private static DateTime GetShutdownOccurrence(DateTime now, string timeOfDay, TimeSpan maxLateness)
    {
        var occurrence = GetOccurrence(now, timeOfDay);
        return now - occurrence > maxLateness ? occurrence.AddDays(1) : occurrence;
    }

    private static IEnumerable<DateTime> GetCandidateOccurrences(DateTime now, string timeOfDay, TimeSpan maxLateness)
    {
        var occurrence = GetOccurrence(now, timeOfDay);
        if (now - occurrence > maxLateness)
        {
            occurrence = occurrence.AddDays(1);
        }

        yield return occurrence;
        yield return occurrence.AddDays(1);
    }

    private static IEnumerable<DateTime> GetShutdownCandidateOccurrences(DateTime now, string timeOfDay, TimeSpan maxLateness)
    {
        var occurrence = GetShutdownOccurrence(now, timeOfDay, maxLateness);
        yield return occurrence;
        yield return occurrence.AddDays(1);
    }

    private static DateOnly GetReminderShutdownDate(DateTime scheduledAt, ReminderSettings reminder)
    {
        var shutdownAt = scheduledAt.AddMinutes(reminder.LeadMinutes.GetValueOrDefault());
        return DateOnly.FromDateTime(shutdownAt);
    }

    private static bool IsDue(DateTime now, DateTime scheduledAt, TimeSpan maxLateness)
    {
        var lateBy = now - scheduledAt;
        return lateBy >= TimeSpan.Zero && lateBy <= maxLateness;
    }

    private static bool IsShutdownDue(DateTime now, DateTime scheduledAt, TimeSpan maxLateness)
    {
        var earlyBy = scheduledAt - now;
        if (earlyBy > TimeSpan.Zero)
        {
            return earlyBy <= ShutdownCountdownLeadTime;
        }

        return now - scheduledAt <= maxLateness;
    }

    private static DateTime Min(DateTime left, DateTime right) => left <= right ? left : right;
}
