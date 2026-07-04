namespace WindowsShutdownTimer.Core;

public sealed class ReminderSettings
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int? LeadMinutes { get; set; }
    public string Time { get; set; } = "23:45";
    public string Message { get; set; } = "";
    public bool Speak { get; set; } = true;
    public bool Toast { get; set; } = true;
    public bool Enabled { get; set; } = true;

    public void Normalize(string shutdownTime)
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            Id = Guid.NewGuid().ToString("N");
        }

        var leadMinutes = LeadMinutes ?? InferLeadMinutes(shutdownTime);
        if (!ReminderScheduleHelper.IsValidLeadMinutes(leadMinutes))
        {
            throw new FormatException("提前提醒时间必须在 1 到 1439 分钟之间。");
        }

        LeadMinutes = leadMinutes;
        Time = ReminderScheduleHelper.GetReminderTime(shutdownTime, leadMinutes);
        Message = ReminderScheduleHelper.BuildReminderMessage(leadMinutes);
    }

    private int InferLeadMinutes(string shutdownTime)
    {
        try
        {
            return ReminderScheduleHelper.MinutesUntil(
                TimeOfDayParser.Parse(Time),
                TimeOfDayParser.Parse(shutdownTime));
        }
        catch
        {
            return 15;
        }
    }
}
