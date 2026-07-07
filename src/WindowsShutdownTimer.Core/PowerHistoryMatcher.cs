namespace WindowsShutdownTimer.Core;

public static class PowerHistoryMatcher
{
    public static IReadOnlyList<PowerHistoryRecord> FilterSince(
        IEnumerable<PowerHistoryRecord> records,
        DateTime since)
    {
        return records
            .Where(record => record.OccurredAt >= since)
            .OrderByDescending(record => record.OccurredAt)
            .ToList();
    }

    public static IReadOnlyList<PowerHistoryRecord> ApplyAutomaticShutdownMarkers(
        IEnumerable<PowerHistoryRecord> records,
        IEnumerable<AutomaticShutdownMarker> markers,
        TimeSpan matchTolerance)
    {
        var availableMarkers = markers
            .OrderBy(marker => marker.TriggeredAt)
            .ToList();

        var matchedMarkerIndexes = new HashSet<int>();
        var result = new List<PowerHistoryRecord>();

        foreach (var record in records.OrderByDescending(record => record.OccurredAt))
        {
            if (record.EventType != PowerEventType.Shutdown)
            {
                result.Add(record);
                continue;
            }

            var markerIndex = FindNearestMarkerIndex(record.OccurredAt, availableMarkers, matchedMarkerIndexes, matchTolerance);
            if (markerIndex < 0)
            {
                result.Add(record);
                continue;
            }

            matchedMarkerIndexes.Add(markerIndex);
            var marker = availableMarkers[markerIndex];
            result.Add(record with
            {
                Origin = ShutdownOrigin.Automatic,
                IsAutomaticShutdown = true,
                Description = BuildAutomaticShutdownDescription(marker)
            });
        }

        return result;
    }

    private static int FindNearestMarkerIndex(
        DateTime occurredAt,
        IReadOnlyList<AutomaticShutdownMarker> markers,
        ISet<int> matchedMarkerIndexes,
        TimeSpan matchTolerance)
    {
        var bestIndex = -1;
        var bestDelta = TimeSpan.MaxValue;

        for (var i = 0; i < markers.Count; i++)
        {
            if (matchedMarkerIndexes.Contains(i))
            {
                continue;
            }

            var delta = (occurredAt - markers[i].TriggeredAt).Duration();
            if (delta <= matchTolerance && delta < bestDelta)
            {
                bestIndex = i;
                bestDelta = delta;
            }
        }

        return bestIndex;
    }

    private static string BuildAutomaticShutdownDescription(AutomaticShutdownMarker marker)
    {
        var mode = marker.ForceShutdown ? "强制关机" : "普通关机";
        return $"本应用定时关机，计划 {marker.ScheduledShutdownTime}，{mode}";
    }
}
