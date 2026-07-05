using System.Text.Json;
using System.Text.Json.Serialization;

namespace WindowsShutdownTimer.Core;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public SettingsStore(string? filePath = null)
    {
        FilePath = filePath ?? GetDefaultFilePath();
    }

    public string FilePath { get; }

    public AppSettings Load()
    {
        if (!File.Exists(FilePath))
        {
            var defaults = AppSettings.CreateDefault();
            Save(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? AppSettings.CreateDefault();
            settings.Normalize();
            return settings;
        }
        catch
        {
            BackupBrokenSettingsFile();
            var defaults = AppSettings.CreateDefault();
            Save(defaults);
            return defaults;
        }
    }

    public void Save(AppSettings settings)
    {
        settings.Normalize();
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public static string GetDefaultFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "WindowsShutdownTimer", "settings.json");
    }

    public static string GetDefaultSettingsFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "WindowsShutdownTimer", "defaults.json");
    }

    private void BackupBrokenSettingsFile()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return;
            }

            var directory = Path.GetDirectoryName(FilePath);
            var fileName = Path.GetFileNameWithoutExtension(FilePath);
            var backupPath = Path.Combine(
                string.IsNullOrWhiteSpace(directory) ? "" : directory,
                $"{fileName}.bad.json");

            File.Copy(FilePath, backupPath, overwrite: true);
        }
        catch
        {
            // Recovery should still continue even if the backup cannot be written.
        }
    }
}
