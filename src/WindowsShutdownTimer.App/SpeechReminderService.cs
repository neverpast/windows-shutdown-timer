using System.Speech.Synthesis;
using System.Media;

namespace WindowsShutdownTimer.App;

public sealed class SpeechReminderService : IDisposable
{
    private readonly SpeechSynthesizer _synthesizer = new();

    public SpeechReminderService()
    {
        _synthesizer.SetOutputToDefaultAudioDevice();
    }

    public void Speak(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        try
        {
            _synthesizer.SpeakAsyncCancelAll();
            _synthesizer.SpeakAsync(message);
        }
        catch
        {
            SystemSounds.Exclamation.Play();
        }
    }

    public void Dispose() => _synthesizer.Dispose();
}
