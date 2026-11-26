using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReadAloud.Models;

namespace ReadAloud.Services;

/// <summary>
/// Online Edge speech engine. Tries the preview voice first, then the ordered
/// priority list; when every attempt fails it silently delegates to Piper.
/// </summary>
public sealed class EdgeSpeechEngine : ISpeechEngine
{
    private readonly Func<AppConfig> _configProvider;
    private readonly IEdgeSynthesizer _synthesizer;
    private readonly IAudioPlayer _player;
    private readonly ISpeechEngine _fallback;
    private readonly EdgeVoiceCatalog _catalog;
    private readonly object _gate = new();
    private CancellationTokenSource? _cancellation;
    private int _generation;
    private int _activeFallbackGeneration;
    private volatile bool _isSpeaking;

    public EdgeSpeechEngine(
        Func<AppConfig> configProvider,
        IEdgeSynthesizer synthesizer,
        IAudioPlayer player,
        ISpeechEngine fallback,
        EdgeVoiceCatalog catalog)
    {
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
        _synthesizer = synthesizer ?? throw new ArgumentNullException(nameof(synthesizer));
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

        // Subscribe once; a fallback read must look identical to an Edge read.
        _fallback.SpeakCompleted += ForwardCompleted;
        _fallback.SpeakProgress += ForwardProgress;
    }

    public event Action? SpeakCompleted;

    public event Action<int, int>? SpeakProgress;

    public bool IsSpeaking => _isSpeaking;

    public IReadOnlyList<VoiceInfo> GetVoices()
    {
        try
        {
            // The library shows the curated Edge voices together with Piper so
            // the user can audition either engine from one list.
            var edge = _catalog.GetVoices();
            var piper = _fallback.GetVoices();
            var seen = new HashSet<string>(edge.Select(voice => voice.Name), StringComparer.OrdinalIgnoreCase);
            return edge.Concat(piper.Where(voice => seen.Add(voice.Name))).ToList();
        }
        catch (Exception)
        {
            return Array.Empty<VoiceInfo>();
        }
    }

    public void Speak(string text, VoiceOptions options)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        try
        {
            // Supersede the previous read before starting a new one.
            Interlocked.Increment(ref _generation);
            Stop();
            var generation = Interlocked.Increment(ref _generation);
            var cancellation = new CancellationTokenSource();
            lock (_gate)
            {
                _cancellation = cancellation;
            }

            _isSpeaking = true;
            _ = Task.Run(() => SpeakAsync(text, options, generation, cancellation));
        }
        catch (Exception)
        {
            _isSpeaking = false;
        }
    }

    public void Stop()
    {
        try
        {
            Interlocked.Increment(ref _generation);
            CancellationTokenSource? cancellation;
            lock (_gate)
            {
                cancellation = _cancellation;
                _cancellation = null;
                _activeFallbackGeneration = 0;
            }

            try
            {
                cancellation?.Cancel();
            }
            catch (Exception)
            {
                // The in-flight read may already have released its token source.
            }

            try
            {
                _player.Stop();
            }
            catch (Exception)
            {
                // Speech failures must never reach the UI layer.
            }

            try
            {
                _fallback.Stop();
            }
            catch (Exception)
            {
                // Speech failures must never reach the UI layer.
            }
        }
        catch (Exception)
        {
            // Speech failures must never reach the UI layer.
        }
        finally
        {
            _isSpeaking = false;
        }
    }

    public void Pause()
    {
        // Edge synthesizes a complete MP3 before playing it; pausing is a no-op.
    }

    public void Resume()
    {
        // Edge synthesizes a complete MP3 before playing it; resuming is a no-op.
    }

    private async Task SpeakAsync(
        string text,
        VoiceOptions options,
        int generation,
        CancellationTokenSource cancellation)
    {
        var token = cancellation.Token;
        string? mp3 = null;
        var usedFallback = false;
        var fallbackFailed = false;

        try
        {
            var config = _configProvider() ?? new AppConfig();
            var rate = FormatRate(config.PlaybackSpeed);

            // A Piper card is previewed locally; it must not be sent to Edge.
            var usePiperPreview = IsFallbackPreview(options);
            if (!usePiperPreview)
            {
                foreach (var voice in Candidates(options, config))
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        var path = await _synthesizer
                            .SynthesizeAsync(text, voice, rate, token)
                            .ConfigureAwait(false);
                        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                        {
                            DeleteTemp(path);
                            continue;
                        }

                        mp3 = path;
                        break;
                    }
                    catch (Exception)
                    {
                        // Try the next voice in the priority list.
                    }
                }
            }

            if (mp3 is null)
            {
                IsCurrent(generation, token);
                usedFallback = true;
                lock (_gate)
                {
                    _activeFallbackGeneration = generation;
                }

                try
                {
                    _fallback.Speak(text, options);
                }
                catch (Exception)
                {
                    // Claim the pending completion ourselves so state cannot stick.
                    lock (_gate)
                    {
                        if (_activeFallbackGeneration == generation)
                        {
                            _activeFallbackGeneration = 0;
                            fallbackFailed = true;
                        }
                    }
                }

                // Piper is asynchronous: completion (and IsSpeaking) now belongs
                // to the forwarded fallback event, not to this worker.
                return;
            }

            IsCurrent(generation, token);
            SpeakProgress?.Invoke(0, 1);
            await Task.Run(() => _player.Play(mp3, options.Volume), token).ConfigureAwait(false);
            IsCurrent(generation, token);
            SpeakProgress?.Invoke(text.Length, 0);
        }
        catch (Exception)
        {
            // Speech failures must never reach the UI layer.
        }
        finally
        {
            DeleteTemp(mp3);
            if (generation == Volatile.Read(ref _generation))
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_cancellation, cancellation))
                    {
                        _cancellation = null;
                    }
                }

                if (!usedFallback || fallbackFailed)
                {
                    _isSpeaking = false;
                    SpeakCompleted?.Invoke();
                }
            }

            cancellation.Dispose();
        }
    }

    private bool IsFallbackPreview(VoiceOptions options)
    {
        var requested = options.VoiceId?.Trim();
        if (string.IsNullOrEmpty(requested))
        {
            return false;
        }

        try
        {
            return _fallback.GetVoices().Any(voice =>
                string.Equals(voice.Name, requested, StringComparison.OrdinalIgnoreCase)
                || string.Equals(voice.Description, requested, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static IEnumerable<string> Candidates(VoiceOptions options, AppConfig config)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var preview = options.VoiceId?.Trim();
        if (!string.IsNullOrEmpty(preview) && seen.Add(preview))
        {
            yield return preview;
        }

        foreach (var configured in config.EdgeVoicePriority ?? new List<string>())
        {
            var voice = configured?.Trim();
            if (!string.IsNullOrEmpty(voice) && seen.Add(voice))
            {
                yield return voice;
            }
        }
    }

    private static string FormatRate(double speed)
        => (PlaybackService.SpeedToRate(speed) * 10)
            .ToString("+0;-0;0", CultureInfo.InvariantCulture);

    private void IsCurrent(int generation, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (generation != Volatile.Read(ref _generation))
        {
            throw new OperationCanceledException();
        }
    }

    private void ForwardCompleted()
    {
        try
        {
            lock (_gate)
            {
                var generation = _activeFallbackGeneration;
                if (generation != 0)
                {
                    _activeFallbackGeneration = 0;
                    if (generation == Volatile.Read(ref _generation))
                    {
                        _isSpeaking = false;
                    }
                }
            }

            SpeakCompleted?.Invoke();
        }
        catch (Exception)
        {
            // Speech failures must never reach the UI layer.
        }
    }

    private void ForwardProgress(int position, int count)
    {
        try
        {
            SpeakProgress?.Invoke(position, count);
        }
        catch (Exception)
        {
            // Speech failures must never reach the UI layer.
        }
    }

    private static void DeleteTemp(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // Temporary files are best-effort.
        }
    }
}
