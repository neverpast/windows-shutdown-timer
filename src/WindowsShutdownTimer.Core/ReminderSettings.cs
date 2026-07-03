namespace WindowsShutdownTimer.Core;

public sealed class ReminderSettings
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Time { get; set; } = "23:45";
    public string Message { get; set; } = "";
    public bool Speak { get; set; } = true;
    public bool Toast { get; set; } = true;
    public bool Enabled { get; set; } = true;

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            Id = Guid.NewGuid().ToString("N");
        }

        Time = TimeOfDayParser.Normalize(Time);
        Message = Message.Trim();
    }
}
