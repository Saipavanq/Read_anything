using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ReadAloud.Models;

namespace ReadAloud.Services;

public sealed class PlaybackService
{
    private static readonly Regex SentenceBoundary = new(@"(?<=[.!?])\s+", RegexOptions.Compiled);
    private static readonly TimeSpan Zero = TimeSpan.Zero;

    private readonly object _gate = new();
    private readonly ISpeechEngine _speech;
    private readonly Func<AppConfig> _configProvider;
    private readonly Action<AppConfig>? _saveConfig;
    private readonly IHistorySink? _history;

    private PlaybackState _state = new(
        string.Empty,
        Array.Empty<string>(),
        0,
        PlaybackStatus.Stopped,
        1.0,
        string.Empty,
        Zero,
        Zero,
        0);

    private string _lastText = string.Empty;
    private IReadOnlyList<string> _lastSentences = Array.Empty<string>();
    private int _lastIndex;
    private VoiceOptions _lastOptions = new(string.Empty, 0, 100);
    private Action? _completedHandler;
    private Action<int, int>? _progressHandler;
    private HistoryEntry? _activeEntry;
    private long _generation;

    public PlaybackService(
        ISpeechEngine speech,
        Func<AppConfig> configProvider,
        Action<AppConfig>? saveConfig = null,
        IHistorySink? history = null)
    {
        _speech = speech;
        _configProvider = configProvider;
        _saveConfig = saveConfig;
        _history = history;
    }

    public event Action<PlaybackState>? StateChanged;

    public PlaybackState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public static int SpeedToRate(double speed)
    {
        var clamped = Math.Clamp(speed, 0.5, 3.0);
        var rate = (int)Math.Round(10 * Math.Log2(clamped), MidpointRounding.AwayFromZero);
        return Math.Clamp(rate, -10, 10);
    }

    public static IReadOnlyList<string> SplitSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        return SentenceBoundary
            .Split(text.Trim())
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)
            .ToArray();
    }

    public Task PlayAsync(
        string text,
        VoiceOptions options,
        string? source = null,
        CaptureMethod captureMethod = CaptureMethod.ClipboardFirst)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Stop();
            return Task.CompletedTask;
        }

        var config = _configProvider();
        var effective = options with { Rate = SpeedToRate(config.PlaybackSpeed) };
        var sentences = SplitSentences(text);
        Start(
            text,
            sentences,
            0,
            effective,
            config.PlaybackSpeed,
            source ?? "manual",
            captureMethod,
            continueHistoryEntry: false);
        return Task.CompletedTask;
    }

    public void Pause()
    {
        if (State.Status != PlaybackStatus.Playing)
        {
            return;
        }

        Supersede();
        _speech.Stop();
        SetState(_state with { Status = PlaybackStatus.Paused });
    }

    public void Resume()
    {
        if (State.Status != PlaybackStatus.Paused)
        {
            return;
        }

        Start(
            _lastText,
            _lastSentences,
            _lastIndex,
            _lastOptions,
            _state.Speed,
            _state.Source,
            _state.CaptureMethod,
            continueHistoryEntry: true);
    }

    public void Stop()
    {
        FinalizeActive("stopped", State.Elapsed);
        Supersede();
        _speech.Stop();
        SetState(new PlaybackState(
            string.Empty,
            Array.Empty<string>(),
            0,
            PlaybackStatus.Stopped,
            _state.Speed,
            string.Empty,
            Zero,
            Zero,
            0));
    }

    public void SkipNext()
    {
        var sentences = State.Sentences;
        if (sentences.Count == 0)
        {
            return;
        }

        var next = Math.Min(State.CurrentSentenceIndex + 1, sentences.Count - 1);
        StartFromSentence(next);
    }

    public void SkipPrevious()
    {
        var sentences = State.Sentences;
        if (sentences.Count == 0)
        {
            return;
        }

        var previous = Math.Max(State.CurrentSentenceIndex - 1, 0);
        StartFromSentence(previous);
    }

    public void SetSpeed(double speed)
    {
        var clamped = Math.Clamp(speed, 0.5, 3.0);
        var config = _configProvider();
        config.PlaybackSpeed = clamped;
        config.SapiRate = SpeedToRate(clamped);
        _saveConfig?.Invoke(config);
        SetState(_state with { Speed = clamped });
    }

    private void StartFromSentence(int index)
    {
        var sentences = State.Sentences;
        var text = string.Join(" ", sentences.Skip(index));
        Start(
            text,
            sentences,
            index,
            _lastOptions,
            _state.Speed,
            _state.Source,
            _state.CaptureMethod,
            continueHistoryEntry: false);
    }

    private void Start(
        string text,
        IReadOnlyList<string> sentences,
        int index,
        VoiceOptions options,
        double speed,
        string source,
        CaptureMethod captureMethod,
        bool continueHistoryEntry)
    {
        if (!continueHistoryEntry)
        {
            FinalizeActive("interrupted", State.Elapsed);
        }

        Supersede();

        var generation = Interlocked.Increment(ref _generation);
        var total = EstimateDuration(text);
        var config = _configProvider();

        SetState(new PlaybackState(
            text,
            sentences,
            index,
            PlaybackStatus.Playing,
            speed,
            options.VoiceName,
            Zero,
            total,
            0,
            source,
            captureMethod));

        if (_activeEntry is null && _history is not null && config.HistoryEnabled)
        {
            _activeEntry = new HistoryEntry(
                Guid.NewGuid().ToString("N"),
                text,
                DateTimeOffset.UtcNow,
                Zero,
                "playing",
                options.VoiceName,
                speed,
                captureMethod.ToString(),
                source);
            _history.Record(_activeEntry);
        }

        void OnCompleted()
        {
            if (generation != Volatile.Read(ref _generation))
            {
                return;
            }

            Detach();
            FinalizeActive("completed", total);
            SetState(_state with
            {
                Status = PlaybackStatus.Completed,
                Elapsed = total,
                EstimatedRemaining = Zero,
                Progress = 1
            });
        }

        void OnProgress(int position, int count)
        {
            if (generation != Volatile.Read(ref _generation))
            {
                return;
            }

            var fraction = Math.Clamp((position + count) / (double)Math.Max(1, text.Length), 0, 1);
            SetState(_state with
            {
                Elapsed = TimeSpan.FromSeconds(total.TotalSeconds * fraction),
                EstimatedRemaining = TimeSpan.FromSeconds(total.TotalSeconds * (1 - fraction)),
                Progress = fraction
            });
        }

        _completedHandler = OnCompleted;
        _progressHandler = OnProgress;
        _speech.SpeakCompleted += OnCompleted;
        _speech.SpeakProgress += OnProgress;

        _lastText = text;
        _lastSentences = sentences;
        _lastIndex = index;
        _lastOptions = options;

        try
        {
            _speech.Speak(text, options);
            if (!_speech.IsSpeaking)
            {
                OnCompleted();
            }
        }
        catch (Exception)
        {
            OnCompleted();
        }
    }

    private void FinalizeActive(string status, TimeSpan duration)
    {
        if (_activeEntry is null)
        {
            return;
        }

        var entry = _activeEntry with { Status = status, Duration = duration };
        _activeEntry = null;
        _history?.Record(entry);
    }

    private void Supersede()
    {
        Interlocked.Increment(ref _generation);
        Detach();
    }

    private void Detach()
    {
        if (_completedHandler is not null)
        {
            _speech.SpeakCompleted -= _completedHandler;
            _completedHandler = null;
        }

        if (_progressHandler is not null)
        {
            _speech.SpeakProgress -= _progressHandler;
            _progressHandler = null;
        }
    }

    private void SetState(PlaybackState state)
    {
        lock (_gate)
        {
            _state = state;
        }

        StateChanged?.Invoke(state);
    }

    private static TimeSpan EstimateDuration(string text)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        return TimeSpan.FromSeconds(Math.Max(1, words / 2.5));
    }
}
