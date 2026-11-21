using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ReadAloud.Models;
using ReadAloud.Services;

namespace ReadAloud.Tests;

public sealed class ReadCoordinatorTests
{
    [Fact]
    public async Task NewestReadWins_AndStopClearsPending()
    {
        var h = new Harness();
        h.Keyboard.OnCtrlC = () => h.Clipboard.Text = $"text{h.Keyboard.SendCalls}";
        var first = h.AddGate();
        var second = h.AddGate();
        var third = h.AddGate();

        var tasks = new[]
        {
            h.Coordinator.RequestReadAsync(),
            h.Coordinator.RequestReadAsync(),
            h.Coordinator.RequestReadAsync()
        };

        Assert.Equal(3, h.Coordinator.PendingCount);

        first.SetResult();
        await tasks[0];
        second.SetResult();
        await tasks[1];
        third.SetResult();
        await tasks[2];

        var spoken = Assert.Single(h.Speech.Spoken);
        Assert.Equal("text3", spoken);
        Assert.Equal(0, h.Coordinator.PendingCount);
    }

    [Fact]
    public async Task ManualText_BypassesCapture()
    {
        var h = new Harness();

        await h.Coordinator.RequestReadAsync("manual text");

        Assert.Equal(0, h.Clipboard.SnapshotCalls);
        Assert.Equal(0, h.Keyboard.SendCalls);
        Assert.Equal("manual text", Assert.Single(h.Speech.Spoken));
    }

    [Fact]
    public async Task EmptyCapture_LogsDiagnosticAndDoesNotSpeak()
    {
        var h = new Harness();
        h.Keyboard.OnCtrlC = () => { };

        await h.Coordinator.RequestReadAsync();

        Assert.Empty(h.Speech.Spoken);
        Assert.Contains(h.Log.Entries, entry => entry.Contains("clipboard unchanged; UIA no selection", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Stop_ClearsPendingAndStopsSpeech()
    {
        var h = new Harness();
        h.Keyboard.OnCtrlC = () => h.Clipboard.Text = "captured";
        var gate = h.AddGate();
        var task = h.Coordinator.RequestReadAsync();

        Assert.Equal(1, h.Coordinator.PendingCount);

        h.Coordinator.Stop();

        Assert.Equal(0, h.Coordinator.PendingCount);
        Assert.Equal(1, h.Speech.StopCalls);

        gate.SetResult();
        await task;
        Assert.Empty(h.Speech.Spoken);
    }

    [Fact]
    public async Task LongText_IsTruncatedToMaxChars()
    {
        var h = new Harness();
        h.Config.MaxChars = 10;

        await h.Coordinator.RequestReadAsync("0123456789abcdefghij");

        Assert.Equal("0123456789", Assert.Single(h.Speech.Spoken));
    }

    private sealed class Harness
    {
        public Harness()
        {
            Clock = new GatedClock();
            Clipboard = new FakeClipboard();
            Keyboard = new FakeKeyboardSender();
            Uia = new FakeUiaSelectionReader();
            Selection = new SelectionService(Clipboard, Keyboard, Uia, Clock);
            Speech = new FakeSpeechEngine();
            Log = new ActivityLog();
            Playback = new PlaybackService(Speech, () => Config);
            Coordinator = new ReadCoordinator(Selection, Playback, Log, () => Config);
        }

        public AppConfig Config { get; } = new();

        public GatedClock Clock { get; }

        public FakeClipboard Clipboard { get; }

        public FakeKeyboardSender Keyboard { get; }

        public FakeUiaSelectionReader Uia { get; }

        public SelectionService Selection { get; }

        public FakeSpeechEngine Speech { get; }

        public PlaybackService Playback { get; }

        public ActivityLog Log { get; }

        public ReadCoordinator Coordinator { get; }

        public TaskCompletionSource AddGate() => Clock.AddGate();
    }

    private sealed class GatedClock : IClock
    {
        private readonly Queue<TaskCompletionSource> _gates = new();

        public TaskCompletionSource AddGate()
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _gates.Enqueue(gate);
            return gate;
        }

        public Task Delay(TimeSpan delay)
            => _gates.Count > 0 ? _gates.Dequeue().Task : Task.CompletedTask;
    }

    private sealed class FakeClipboard : IClipboard
    {
        public FakeClipboard()
        {
            Text = "previous clipboard";
        }

        public string? Text { get; set; }

        public int SnapshotCalls { get; private set; }

        public Dictionary<int, object?> Snapshot()
        {
            SnapshotCalls++;
            return new Dictionary<int, object?> { [1] = Text };
        }

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
        public Action? OnCtrlC { get; set; }

        public int SendCalls { get; private set; }

        public void SendCtrlC()
        {
            SendCalls++;
            OnCtrlC?.Invoke();
        }
    }

    private sealed class FakeUiaSelectionReader : IUiaSelectionReader
    {
        public string? Result { get; set; }

        public Task<string?> TryReadSelectionAsync(TimeSpan timeout) => Task.FromResult(Result);
    }

    private sealed class FakeSpeechEngine : ISpeechEngine
    {
        public event Action? SpeakCompleted;

        public event Action<int, int>? SpeakProgress;

        public List<string> Spoken { get; } = new();

        public int StopCalls { get; private set; }

        public bool IsSpeaking => false;

        public IReadOnlyList<VoiceInfo> GetVoices() => Array.Empty<VoiceInfo>();

        public void Speak(string text, VoiceOptions options) => Spoken.Add(text);

        public void Stop() => StopCalls++;
    }
}
