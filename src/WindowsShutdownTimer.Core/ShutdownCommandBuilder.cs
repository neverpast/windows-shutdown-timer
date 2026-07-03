namespace WindowsShutdownTimer.Core;

public static class ShutdownCommandBuilder
{
    public const string FileName = "shutdown.exe";

    public static string BuildArguments(bool force) => force ? "/s /t 0 /f" : "/s /t 0";
}
