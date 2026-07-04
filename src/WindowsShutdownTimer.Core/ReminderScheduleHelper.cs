namespace WindowsShutdownTimer.Core;

public static class ReminderScheduleHelper
{
    private const int MinutesPerDay = 24 * 60;

    public static void ShiftRemindersForShutdownChange(
        string oldShutdownTime,
        string newShutdownTime,
        IEnumerable<ReminderSettings> reminders)
    {
        var oldShutdown = TimeOfDayParser.Parse(oldShutdownTime);
        var newShutdown = TimeOfDayParser.Parse(newShutdownTime);

        foreach (var reminder in reminders)
        {
            var oldReminder = TimeOfDayParser.Parse(reminder.Time);
            var offsetMinutes = MinutesUntil(oldReminder, oldShutdown);
            var newReminder = NormalizeMinutes((int)newShutdown.TotalMinutes - offsetMinutes);

            reminder.Time = FormatMinutes(newReminder);
            reminder.Message = $"还有{FormatOffset(offsetMinutes)}自动关机";
        }
    }

    public static int MinutesUntil(TimeSpan from, TimeSpan to)
    {
        return NormalizeMinutes((int)to.TotalMinutes - (int)from.TotalMinutes);
    }

    private static int NormalizeMinutes(int minutes)
    {
        var normalized = minutes % MinutesPerDay;
        return normalized < 0 ? normalized + MinutesPerDay : normalized;
    }

    private static string FormatMinutes(int minutes)
    {
        var hour = minutes / 60;
        var minute = minutes % 60;
        return $"{hour:00}:{minute:00}";
    }

    private static string FormatOffset(int minutes)
    {
        if (minutes <= 0)
        {
            return "0分钟";
        }

        var hours = minutes / 60;
        var remainingMinutes = minutes % 60;

        if (hours == 0)
        {
            return $"{remainingMinutes}分钟";
        }

        return remainingMinutes == 0
            ? $"{hours}小时"
            : $"{hours}小时{remainingMinutes}分钟";
    }
}
