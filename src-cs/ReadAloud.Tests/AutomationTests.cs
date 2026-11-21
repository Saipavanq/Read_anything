using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ReadAloud.Models;
using ReadAloud.Services;

namespace ReadAloud.Tests;

public sealed class AutomationTests
{
    [Fact]
    public void AutoReadDisabled_NoTriggerStarted()
    {
        var h = new Harness();
        h.Config.AutoReadSelection = false;

        h.Service.ApplyConfig();

        Assert.False(h.Service.IsRunning);
        Assert.Equal(0, h.Hook.StartCalls);
    }

    [Fact]
    public void AutoReadEnabled_TriggerStarted()
    {
        var h = new Harness();
        h.Config.AutoReadSelection = true;

        h.Service.ApplyConfig();

        Assert.True(h.Service.IsRunning);
        Assert.Equal(1, h.Hook.StartCalls);
    }

    [Fact]
    public async Task FakeMouseUp_CallsCoordinatorOnceAfterDebounce()
    {
        var h = new Harness();
        h.Config.AutoReadSelection = true;
        h.Service.ApplyConfig();

        h.Hook.RaiseMouseUp();
        h.Hook.RaiseMouseUp();
        Assert.Single(h.Clock.Gates);
        Assert.Null(h.Service.LastReadTask);

        h.Clock.Gates[0].SetResult();
        await h.Service.LastReadTask!;

        Assert.Single(h.Speech.Spoken);
    }

    [Fact]
    public async Task TriggeredRead_UsesDispatcher()
    {
        var h = new Harness(useDispatcher: true);
        h.Config.AutoReadSelection = true;
        h.Service.ApplyConfig();

        h.Hook.RaiseMouseUp();
        h.Clock.Gates[0].SetResult();
        await h.Service.LastReadTask!;

        Assert.Equal(1, h.DispatchCalls);
        Assert.Single(h.Speech.Spoken);
    }

    [Fact]
    public void OwnWindowForeground_EventIgnored()
    {
        var h = new Harness();
        h.Config.AutoReadSelection = true;
        h.ForegroundOwnWindow = true;
        h.Service.ApplyConfig();

        h.Hook.RaiseMouseUp();

        Assert.Empty(h.Clock.Gates);
        Assert.Null(h.Service.LastReadTask);
    }

    [Fact]
    public void DisablingAutoRead_StopsTrigger()
    {
        var h = new Harness();
        h.Config.AutoReadSelection = true;
        h.Service.ApplyConfig();

        h.Config.AutoReadSelection = false;
        h.Service.ApplyConfig();

        Assert.False(h.Service.IsRunning);
        Assert.Equal(1, h.Hook.StopCalls);

        h.Hook.RaiseMouseUp();
        Assert.Null(h.Service.LastReadTask);
    }

    private sealed class Harness
    {
        public Harness(bool useDispatcher = false)
        {
            Config = new AppConfig();
            Hook = new FakeMouseHook();
            Clock = new ManualClock();
            Trigger = new MouseUpAutoReadTrigger(() => ForegroundOwnWindow, Hook, Clock);
            Speech = new FakeSpeechEngine();
            Playback = new PlaybackService(Speech, () => Config);
            Selection = new SelectionService(
                new FakeClipboard(),
                new FakeKeyboardSender(),
                new FakeUiaSelectionReader(),
                new ImmediateClock());
            Log = new ActivityLog();
            Coordinator = new ReadCoordinator(Selection, Playback, Log, () => Config);
            Service = useDispatcher
                ? new AutoReadService(Trigger, Coordinator, () => Config, action =>
                {
                    DispatchCalls++;
                    action();
                })
                : new AutoReadService(Trigger, Coordinator, () => Config);
        }

        public int DispatchCalls { get; private set; }

        public AppConfig Config { get; }

        public bool ForegroundOwnWindow { get; set; }

        public FakeMouseHook Hook { get; }

        public ManualClock Clock { get; }

        public MouseUpAutoReadTrigger Trigger { get; }

        public FakeSpeechEngine Speech { get; }

        public PlaybackService Playback { get; }

        public SelectionService Selection { get; }

        public ActivityLog Log { get; }

        public ReadCoordinator Coordinator { get; }

        public AutoReadService Service { get; }
    }

    private sealed class FakeMouseHook : IMouseHook
    {
        public event Action? MouseUp;

        public int StartCalls { get; private set; }

        public int StopCalls { get; private set; }

        public bool IsStarted { get; private set; }

        public void Start()
        {
            StartCalls++;
            IsStarted = true;
        }

        public void Stop()
        {
            StopCalls++;
            IsStarted = false;
        }

        public void RaiseMouseUp() => MouseUp?.Invoke();
    }

    private sealed class ManualClock : IClock
    {
        public List<TaskCompletionSource> Gates { get; } = new();

        public Task Delay(TimeSpan delay)
        {
            var gate = new TaskCompletionSource();
            Gates.Add(gate);
            return gate.Task;
        }
    }

    private sealed class ImmediateClock : IClock
    {
        public Task Delay(TimeSpan delay) => Task.CompletedTask;
    }

    private sealed class FakeClipboard : IClipboard
    {
        public string? Text { get; set; } = "selected text";

        public Dictionary<int, object?> Snapshot() => new() { [1] = Text };

        public void Restore(Dictionary<int, object?> snapshot)
        {
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
        public Task<string?> TryReadSelectionAsync(TimeSpan timeout) => Task.FromResult<string?>("selected text");
    }

    private sealed class FakeSpeechEngine : ISpeechEngine
    {
        public event Action? SpeakCompleted;

        public event Action<int, int>? SpeakProgress;

        public bool IsSpeaking => false;

        public List<string> Spoken { get; } = new();

        public IReadOnlyList<VoiceInfo> GetVoices() => Array.Empty<VoiceInfo>();

        public void Speak(string text, VoiceOptions options) => Spoken.Add(text);

        public void Stop()
        {
        }
    }
}
