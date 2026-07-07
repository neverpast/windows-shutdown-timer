using System.Text.Json;
using System.Text.Json.Serialization;

namespace WindowsShutdownTimer.Core;

public sealed class AutomaticShutdownMarkerStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public AutomaticShutdownMarkerStore(string? filePath = null)
    {
        FilePath = filePath ?? GetDefaultFilePath();
    }

    public string FilePath { get; }

    public List<AutomaticShutdownMarker> Load()
    {
        if (!File.Exists(FilePath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            var markers = JsonSerializer.Deserialize<List<AutomaticShutdownMarker>>(json, JsonOptions) ?? [];
            return markers
                .Where(marker => marker.TriggeredAt != default)
                .OrderByDescending(marker => marker.TriggeredAt)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public void Save(IEnumerable<AutomaticShutdownMarker> markers)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var data = markers
            .Where(marker => marker.TriggeredAt != default)
            .OrderByDescending(marker => marker.TriggeredAt)
            .ToList();

        File.WriteAllText(FilePath, JsonSerializer.Serialize(data, JsonOptions));
    }

    public void Add(AutomaticShutdownMarker marker)
    {
        var markers = Load();
        markers.Add(marker);
        Save(markers);
    }

    public static string GetDefaultFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "WindowsShutdownTimer", "shutdown-markers.json");
    }
}
