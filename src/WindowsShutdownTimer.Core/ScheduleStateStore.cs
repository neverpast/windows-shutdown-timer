using System.Text.Json;
using System.Text.Json.Serialization;

namespace WindowsShutdownTimer.Core;

public sealed class ScheduleStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ScheduleStateStore(string? filePath = null)
    {
        FilePath = filePath ?? GetDefaultFilePath();
    }

    public string FilePath { get; }

    public ScheduleState Load()
    {
        if (!File.Exists(FilePath))
        {
            return new ScheduleState();
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            var data = JsonSerializer.Deserialize<ScheduleStateData>(json, JsonOptions);
            var state = new ScheduleState();

            if (data?.PausedShutdownDate is { } pausedDate)
            {
                state.PauseShutdownFor(pausedDate);
            }

            return state;
        }
        catch
        {
            return new ScheduleState();
        }
    }

    public void Save(ScheduleState state)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var data = new ScheduleStateData
        {
            PausedShutdownDate = state.PausedShutdownDate
        };

        File.WriteAllText(FilePath, JsonSerializer.Serialize(data, JsonOptions));
    }

    public static string GetDefaultFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "WindowsShutdownTimer", "state.json");
    }

    private sealed class ScheduleStateData
    {
        public DateOnly? PausedShutdownDate { get; set; }
    }
}
