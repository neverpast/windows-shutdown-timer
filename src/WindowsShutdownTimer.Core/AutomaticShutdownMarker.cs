namespace WindowsShutdownTimer.Core;

public sealed record AutomaticShutdownMarker(
    DateTime TriggeredAt,
    string ScheduledShutdownTime,
    bool ForceShutdown);
