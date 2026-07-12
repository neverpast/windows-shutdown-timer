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

    public Task SpeakShutdownCountdownAsync(DateTime shutdownAt, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                lock (_syncRoot)
                {
                    _synthesizer.SpeakAsyncCancelAll();

                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var remainingSeconds = (int)Math.Ceiling((shutdownAt - DateTime.Now).TotalSeconds);
                        if (remainingSeconds <= 0)
                        {
                            return;
                        }

                        var number = Math.Min(remainingSeconds, 10);
                        _synthesizer.Speak(number.ToString());

                        var delay = shutdownAt.AddSeconds(-(number - 1)) - DateTime.Now;
                        if (delay > TimeSpan.Zero)
                        {
                            cancellationToken.WaitHandle.WaitOne(delay);
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
                var remaining = shutdownAt - DateTime.Now;
                if (remaining > TimeSpan.Zero)
                {
                    cancellationToken.WaitHandle.WaitOne(remaining);
                }
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
