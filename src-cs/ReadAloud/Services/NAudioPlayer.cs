using System;
using System.Threading;
using NAudio.Wave;

namespace ReadAloud.Services;

/// <summary>
/// Local MP3 playback boundary. Tests replace this seam so they never touch an
/// audio device.
/// </summary>
public interface IAudioPlayer
{
    void Play(string file, int volume);

    void Stop();
}

public sealed class NAudioPlayer : IAudioPlayer
{
    private readonly object _gate = new();
    private int _generation;
    private bool _stopRequested;
    private WaveOutEvent? _output;
    private AudioFileReader? _reader;
    private ManualResetEventSlim? _finished;

    public void Play(string file, int volume)
    {
        RequestStop();
        var generation = Interlocked.Increment(ref _generation);
        lock (_gate)
        {
            _stopRequested = false;
        }

        AudioFileReader? reader = null;
        WaveOutEvent? output = null;
        ManualResetEventSlim? finished = null;
        EventHandler<StoppedEventArgs>? handler = null;

        try
        {
            reader = new AudioFileReader(file);
            output = new WaveOutEvent();
            finished = new ManualResetEventSlim(false);
            handler = (_, _) => SignalFinished(generation, finished);
            output.PlaybackStopped += handler;
            output.Volume = Math.Clamp(volume, 0, 100) / 100f;
            output.Init(reader);

            lock (_gate)
            {
                // A Stop during construction supersedes this playback before a
                // device is ever started.
                if (_stopRequested || generation != _generation)
                {
                    return;
                }

                _reader = reader;
                _output = output;
                _finished = finished;
            }

            output.Play();
            finished.Wait(PlaybackTimeout(reader));
        }
        catch (Exception)
        {
            // Playback failures must never reach the UI layer.
        }
        finally
        {
            Release(generation, ref reader, ref output, ref finished, handler);
        }
    }

    public void Stop()
    {
        RequestStop();
    }

    private void RequestStop()
    {
        WaveOutEvent? output;
        ManualResetEventSlim? finished;
        lock (_gate)
        {
            Interlocked.Increment(ref _generation);
            _stopRequested = true;
            output = _output;
            finished = _finished;
        }

        try
        {
            output?.Stop();
        }
        catch (Exception)
        {
            // The device may already have been released.
        }

        lock (_gate)
        {
            if (ReferenceEquals(_finished, finished))
            {
                try
                {
                    finished?.Set();
                }
                catch (ObjectDisposedException)
                {
                    // The handler must never call into a disposed event.
                }
            }
        }
    }

    private void SignalFinished(int generation, ManualResetEventSlim? finished)
    {
        lock (_gate)
        {
            if (generation != _generation || finished is null
                || !ReferenceEquals(_finished, finished))
            {
                return;
            }

            try
            {
                finished.Set();
            }
            catch (ObjectDisposedException)
            {
                // The event may be disposed by a concurrent Release.
            }
        }
    }

    private static TimeSpan PlaybackTimeout(AudioFileReader reader)
    {
        var bytesPerSecond = reader.WaveFormat.AverageBytesPerSecond;
        var seconds = bytesPerSecond > 0
            ? reader.Length / (double)bytesPerSecond + 5
            : 60;
        return TimeSpan.FromSeconds(Math.Clamp(seconds, 5, 3600));
    }

    private void Release(
        int generation,
        ref AudioFileReader? reader,
        ref WaveOutEvent? output,
        ref ManualResetEventSlim? finished,
        EventHandler<StoppedEventArgs>? handler)
    {
        lock (_gate)
        {
            if (generation == _generation)
            {
                _output = null;
                _reader = null;
                _finished = null;
            }

            if (output is not null && handler is not null)
            {
                try
                {
                    output.PlaybackStopped -= handler;
                }
                catch (Exception)
                {
                    // Device cleanup is best-effort.
                }
            }
        }

        try
        {
            output?.Dispose();
        }
        catch (Exception)
        {
            // Device cleanup is best-effort.
        }

        try
        {
            reader?.Dispose();
        }
        catch (Exception)
        {
            // File cleanup is best-effort.
        }

        try
        {
            finished?.Dispose();
        }
        catch (Exception)
        {
            // Synchronization cleanup is best-effort.
        }
    }
}
