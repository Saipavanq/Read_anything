using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ReadAloud.Models;
using ReadAloud.Services;
using ReadAloud.ViewModels;

namespace ReadAloud.Tests;

public sealed class HistoryTests
{
    [Fact]
    public async Task DisabledHistory_RecordsNothing()
    {
        var h = new Harness();
        h.Config.HistoryEnabled = false;
        h.History.SetEnabled(true);

        await h.Playback.PlayAsync("Hello there.", VoiceOptions.FromConfig(h.Config));

        Assert.Empty(h.History.Entries);
    }

    [Fact]
    public async Task EnabledHistory_StoresTruncatedSpokenText()
    {
        var h = new Harness(enabled: true);
        h.Config.MaxChars = 20;

        await h.Coordinator.RequestReadAsync("0123456789abcdefghijklmnop");

        var entry = Assert.Single(h.History.Entries);
        Assert.Equal("0123456789abcdefghij", entry.Text);
        Assert.Equal("manual", entry.Source);
    }

    [Fact]
    public async Task Duration_RecordedForCompletedStoppedInterrupted()
    {
        var completed = new Harness(enabled: true);
        completed.Speech.AutoComplete = false;
        await completed.Playback.PlayAsync("Completed spoken text.", VoiceOptions.FromConfig(completed.Config));
        completed.Speech.RaiseCompleted();
        Assert.Equal("completed", completed.History.Entries.Single().Status);
        Assert.True(completed.History.Entries.Single().Duration > TimeSpan.Zero);

        var stopped = new Harness(enabled: true);
        stopped.Speech.AutoComplete = false;
        await stopped.Playback.PlayAsync("Stopped spoken text.", VoiceOptions.FromConfig(stopped.Config));
        stopped.Speech.RaiseProgress(stopped.Speech.LastText!.Length / 2, 1);
        stopped.Playback.Stop();
        Assert.Equal("stopped", stopped.History.Entries.Single().Status);
        Assert.True(stopped.History.Entries.Single().Duration > TimeSpan.Zero);

        var interrupted = new Harness(enabled: true);
        interrupted.Speech.AutoComplete = false;
        await interrupted.Playback.PlayAsync("First spoken text.", VoiceOptions.FromConfig(interrupted.Config));
        interrupted.Speech.RaiseProgress(interrupted.Speech.LastText!.Length / 2, 1);
        await interrupted.Playback.PlayAsync("Second spoken text.", VoiceOptions.FromConfig(interrupted.Config));
        Assert.Equal("interrupted", interrupted.History.Entries.Last().Status);
        Assert.True(interrupted.History.Entries.Last().Duration > TimeSpan.Zero);
    }

    [Fact]
    public async Task Replay_CreatesNewEntryWithSourceReplay()
    {
        var h = new Harness(enabled: true);
        await h.Coordinator.RequestReadAsync("Original text.");
        var original = h.History.Entries.Single();

        await h.ViewModel.ReplayAsync(original);

        Assert.Equal(2, h.History.Entries.Count);
        Assert.Equal(original.Text, h.History.Entries[0].Text);
        Assert.Equal("replay", h.History.Entries[0].Source);
        Assert.Equal(original.CaptureMethod, h.History.Entries[0].CaptureMethod);
    }

    [Fact]
    public void DeleteDeleteAllAndRetention_Work()
    {
        var h = new Harness(enabled: true);
        var old = Entry("old", DateTimeOffset.UtcNow.AddDays(-10));
        var recent = Entry("recent", DateTimeOffset.UtcNow);
        h.History.Add(old);
        h.History.Add(recent);
        Assert.Equal(2, h.History.Entries.Count);

        h.History.Delete(old.Id);
        Assert.Equal("recent", Assert.Single(h.History.Entries).Text);

        h.History.DeleteAll();
        Assert.Empty(h.History.Entries);

        h.History.Add(old);
        h.History.Add(recent);
        h.History.ApplyRetention(3);
        Assert.Equal("recent", Assert.Single(h.History.Entries).Text);
    }

    [Fact]
    public void CorruptHistoryFile_FallsBackToEmpty()
    {
        var path = Path.Combine(Path.GetTempPath(), "ReadAloudHistory_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, "{ not json");

            var history = new HistoryService(path);

            Assert.Empty(history.Entries);
            Assert.False(history.IsEnabled);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static HistoryEntry Entry(string text, DateTimeOffset timestamp)
        => new(
            Guid.NewGuid().ToString("N"),
            text,
            timestamp,
            TimeSpan.FromSeconds(2),
            "completed",
            "Voice",
            1.0,
            "ClipboardFirst",
            "manual");

    private sealed class Harness : IDisposable
    {
        private readonly string _directory;

        public Harness(bool enabled = false)
        {
            _directory = Path.Combine(Path.GetTempPath(), "ReadAloudHistoryTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            Config = new AppConfig { HistoryEnabled = enabled };
            History = new HistoryService(Path.Combine(_directory, "history.json"));
            History.SetEnabled(enabled);
            Speech = new FakeSpeechEngine();
            Playback = new PlaybackService(Speech, () => Config, null, History);
            Selection = new SelectionService(new FakeClipboard(), new FakeKeyboardSender(), new FakeUiaSelectionReader(), new FakeClock());
            Log = new ActivityLog();
            Coordinator = new ReadCoordinator(Selection, Playback, Log, () => Config);
            ViewModel = new HistoryViewModel(History, Coordinator);
        }

        public AppConfig Config { get; }

        public HistoryService History { get; }

        public FakeSpeechEngine Speech { get; }

        public PlaybackService Playback { get; }

        public SelectionService Selection { get; }

        public ActivityLog Log { get; }

        public ReadCoordinator Coordinator { get; }

        public HistoryViewModel ViewModel { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private sealed class FakeClock : IClock
    {
        public Task Delay(TimeSpan delay) => Task.CompletedTask;
    }

    private sealed class FakeClipboard : IClipboard
    {
        public string? Text { get; set; } = "previous clipboard";

        public Dictionary<int, object?> Snapshot() => new() { [1] = Text };

        public void Restore(Dictionary<int, object?> snapshot)
        {
            if (snapshot.TryGetValue(1, out var value))
            {
                Text = value as string;
            }
        }

        public string? GetText() => Text;

        public void SetText(string text) => Text = text;
    }

    private sealed class FakeKeyboardSender : IKeyboardSender
    {
        public void SendCtrlC()
        {
        }
    }

    private sealed class FakeUiaSelectionReader : IUiaSelectionReader
    {
        public Task<string?> TryReadSelectionAsync(TimeSpan timeout) => Task.FromResult<string?>(null);
    }

    private sealed class FakeSpeechEngine : ISpeechEngine
    {
        private Action? _speakCompleted;
        private Action<int, int>? _speakProgress;

        public event Action? SpeakCompleted
        {
            add => _speakCompleted += value;
            remove => _speakCompleted -= value;
        }

        public event Action<int, int>? SpeakProgress
        {
            add => _speakProgress += value;
            remove => _speakProgress -= value;
        }

        public bool AutoComplete { get; set; } = true;

        public bool IsSpeaking { get; private set; }

        public string? LastText { get; private set; }

        public IReadOnlyList<VoiceInfo> GetVoices() => Array.Empty<VoiceInfo>();

        public void Speak(string text, VoiceOptions options)
        {
            LastText = text;

            if (AutoComplete)
            {
                IsSpeaking = false;
                _speakCompleted?.Invoke();
            }
            else
            {
                IsSpeaking = true;
            }
        }

        public void Stop() => IsSpeaking = false;

        public void RaiseCompleted()
        {
            IsSpeaking = false;
            _speakCompleted?.Invoke();
        }

        public void RaiseProgress(int position, int count) => _speakProgress?.Invoke(position, count);
    }
}
