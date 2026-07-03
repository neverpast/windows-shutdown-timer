using System.Globalization;

namespace WindowsShutdownTimer.Core;

public static class TimeOfDayParser
{
    public static TimeSpan Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException("时间不能为空。");
        }

        var text = value.Trim();
        var parts = text.Split(':', StringSplitOptions.TrimEntries);

        if (parts.Length != 2 ||
            !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hour) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minute))
        {
            throw new FormatException($"时间格式无效：{value}。请使用 HH:mm。");
        }

        if (hour == 24 && minute == 0)
        {
            return TimeSpan.Zero;
        }

        if (hour < 0 || hour > 23 || minute < 0 || minute > 59)
        {
            throw new FormatException($"时间超出范围：{value}。请使用 00:00 到 23:59，或 24:00。");
        }

        return new TimeSpan(hour, minute, 0);
    }

    public static string Normalize(string value)
    {
        var time = Parse(value);
        return $"{(int)time.TotalHours:00}:{time.Minutes:00}";
    }
}
