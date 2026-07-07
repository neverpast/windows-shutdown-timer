namespace WindowsShutdownTimer.Core;

public sealed record PowerHistoryRecord(
    DateTime OccurredAt,
    PowerEventType EventType,
    ShutdownOrigin Origin,
    bool IsAutomaticShutdown,
    string Description);
