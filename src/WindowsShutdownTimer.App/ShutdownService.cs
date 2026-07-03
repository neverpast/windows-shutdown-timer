using System.Diagnostics;
using WindowsShutdownTimer.Core;

namespace WindowsShutdownTimer.App;

public sealed class ShutdownService
{
    public void Shutdown(bool force)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = ShutdownCommandBuilder.FileName,
            Arguments = ShutdownCommandBuilder.BuildArguments(force),
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }
}
