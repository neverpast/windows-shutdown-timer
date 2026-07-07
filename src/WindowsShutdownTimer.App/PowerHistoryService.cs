using System.Diagnostics.Eventing.Reader;
using WindowsShutdownTimer.Core;

namespace WindowsShutdownTimer.App;

public sealed class PowerHistoryService
{
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MarkerMatchTolerance = TimeSpan.FromMinutes(5);

    private readonly AutomaticShutdownMarkerStore _markerStore;

    public PowerHistoryService(AutomaticShutdownMarkerStore markerStore)
    {
        _markerStore = markerStore;
    }

    public IReadOnlyList<PowerHistoryRecord> GetRecentHistory(TimeSpan range)
    {
        try
        {
            var since = DateTime.Now.Subtract(range);
            var systemRecords = PowerHistoryMatcher.FilterSince(ReadSystemRecords(range), since);
            var markers = _markerStore.Load()
                .Where(marker => marker.TriggeredAt >= since.Subtract(MarkerMatchTolerance))
                .ToList();

            return PowerHistoryMatcher.ApplyAutomaticShutdownMarkers(
                systemRecords,
                markers,
                MarkerMatchTolerance);
        }
        catch (Exception ex) when (ex is EventLogException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException("无法读取系统事件日志，请确认当前用户有权限读取 Windows System 日志。", ex);
        }
    }

    private static IReadOnlyList<PowerHistoryRecord> ReadSystemRecords(TimeSpan range)
    {
        var since = DateTime.Now.Subtract(range);
        var queryText =
            "*[System[" +
            "(EventID=12 or EventID=13 or EventID=1074 or EventID=6005 or EventID=6006)" +
            "]]";
        var query = new EventLogQuery("System", PathType.LogName, queryText)
        {
            ReverseDirection = true
        };

        var events = new List<SystemPowerEvent>();
        using var reader = new EventLogReader(query);
        EventRecord? eventRecord;
        while ((eventRecord = reader.ReadEvent()) is not null)
        {
            using (eventRecord)
            {
                var powerEvent = TryReadPowerEvent(eventRecord);
                if (powerEvent is not null)
                {
                    if (powerEvent.OccurredAt < since)
                    {
                        break;
                    }

                    events.Add(powerEvent);
                }
            }
        }

        return CollapseDuplicateEvents(events)
            .Select(powerEvent => new PowerHistoryRecord(
                powerEvent.OccurredAt,
                powerEvent.EventType,
                ShutdownOrigin.ManualOrSystem,
                IsAutomaticShutdown: false,
                powerEvent.Description))
            .OrderByDescending(record => record.OccurredAt)
            .ToList();
    }

    private static SystemPowerEvent? TryReadPowerEvent(EventRecord eventRecord)
    {
        if (eventRecord.TimeCreated is not { } occurredAt)
        {
            return null;
        }

        var eventType = eventRecord.Id is 12 or 6005
            ? PowerEventType.Startup
            : PowerEventType.Shutdown;
        var description = BuildDescription(eventRecord.Id, eventType, eventRecord);
        return new SystemPowerEvent(occurredAt, eventType, eventRecord.Id, description);
    }

    private static string BuildDescription(int eventId, PowerEventType eventType, EventRecord eventRecord)
    {
        if (eventId == 1074)
        {
            return CleanDescription(SafeFormatDescription(eventRecord)) ?? "Windows 关机或重启请求";
        }

        return eventType switch
        {
            PowerEventType.Startup => eventId == 12 ? "Windows 系统启动" : "Windows 事件日志服务启动",
            _ => eventId == 13 ? "Windows 系统正在关机" : "Windows 正常关机"
        };
    }

    private static string? SafeFormatDescription(EventRecord eventRecord)
    {
        try
        {
            return eventRecord.FormatDescription();
        }
        catch
        {
            return null;
        }
    }

    private static string? CleanDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var cleaned = string.Join(" ", description.Split(
            new[] { ' ', '\r', '\n', '\t' },
            StringSplitOptions.RemoveEmptyEntries));
        return cleaned.Length > 220 ? $"{cleaned[..217]}..." : cleaned;
    }

    private static IReadOnlyList<SystemPowerEvent> CollapseDuplicateEvents(IEnumerable<SystemPowerEvent> events)
    {
        var result = new List<SystemPowerEvent>();

        foreach (var group in events
            .GroupBy(powerEvent => powerEvent.EventType)
            .SelectMany(group => BuildDuplicateGroups(group.OrderByDescending(powerEvent => powerEvent.OccurredAt))))
        {
            result.Add(group
                .OrderByDescending(GetEventPriority)
                .ThenByDescending(powerEvent => powerEvent.OccurredAt)
                .First());
        }

        return result;
    }

    private static IEnumerable<List<SystemPowerEvent>> BuildDuplicateGroups(IEnumerable<SystemPowerEvent> events)
    {
        var current = new List<SystemPowerEvent>();

        foreach (var powerEvent in events)
        {
            if (current.Count == 0 || (current[0].OccurredAt - powerEvent.OccurredAt).Duration() <= DuplicateWindow)
            {
                current.Add(powerEvent);
                continue;
            }

            yield return current;
            current = [powerEvent];
        }

        if (current.Count > 0)
        {
            yield return current;
        }
    }

    private static int GetEventPriority(SystemPowerEvent powerEvent)
    {
        return powerEvent.EventId switch
        {
            1074 => 4,
            12 => 3,
            13 => 2,
            6006 => 1,
            6005 => 1,
            _ => 0
        };
    }

    private sealed record SystemPowerEvent(
        DateTime OccurredAt,
        PowerEventType EventType,
        int EventId,
        string Description);
}
