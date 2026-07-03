using System.Speech.Synthesis;
using System.Media;

namespace WindowsShutdownTimer.App;

public sealed class SpeechReminderService : IDisposable
{
    private readonly SpeechSynthesizer _synthesizer = new();
    private readonly object _syncRoot = new();

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
            lock (_syncRoot)
            {
                _synthesizer.SpeakAsyncCancelAll();
                _synthesizer.SpeakAsync(message);
            }
        }
        catch
        {
            SystemSounds.Exclamation.Play();
        }
    }

    public Task SpeakShutdownCountdownAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                lock (_syncRoot)
                {
                    _synthesizer.SpeakAsyncCancelAll();

                    for (var number = 10; number >= 1; number--)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var started = DateTime.UtcNow;
                        _synthesizer.Speak(number.ToString());

                        var remaining = TimeSpan.FromSeconds(1) - (DateTime.UtcNow - started);
                        if (remaining > TimeSpan.Zero)
                        {
                            cancellationToken.WaitHandle.WaitOne(remaining);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                SystemSounds.Exclamation.Play();
                Thread.Sleep(TimeSpan.FromSeconds(10));
            }
        }, cancellationToken);
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            _synthesizer.Dispose();
        }
    }
}
