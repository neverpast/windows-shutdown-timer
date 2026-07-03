namespace WindowsShutdownTimer.Core;

public sealed class ScheduleState
{
    private readonly HashSet<string> _firedKeys = [];

    public DateOnly? PausedShutdownDate { get; private set; }

    public void PauseShutdownFor(DateOnly date) => PausedShutdownDate = date;

    public void ResumeShutdown() => PausedShutdownDate = null;

    public bool IsShutdownPaused(DateOnly date) => PausedShutdownDate == date;

    public bool HasFired(string key) => _firedKeys.Contains(key);

    public void MarkFired(string key) => _firedKeys.Add(key);

    public void Trim(DateTime now)
    {
        var cutoff = DateOnly.FromDateTime(now.Date.AddDays(-2));
        _firedKeys.RemoveWhere(key =>
        {
            var lastColon = key.LastIndexOf(':');
            if (lastColon < 0 || lastColon == key.Length - 1)
            {
                return false;
            }

            return DateOnly.TryParseExact(key[(lastColon + 1)..], "yyyy-MM-dd", out var date) && date < cutoff;
        });

        if (PausedShutdownDate is { } paused && paused < DateOnly.FromDateTime(now.Date))
        {
            PausedShutdownDate = null;
        }
    }
}
