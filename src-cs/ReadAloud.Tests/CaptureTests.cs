using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ReadAloud.Models;
using ReadAloud.Services;

namespace ReadAloud.Tests;

public sealed class CaptureTests
{
    [Fact]
    public async Task ClipboardFirst_ReturnsText_WhenUiaHasNothing()
    {
        var h = new Harness();
        h.Keyboard.OnCtrlC = () => h.Clipboard.Text = "Selected text";

        var result = await h.Service.CaptureAsync(CaptureMethod.ClipboardFirst);

        Assert.Equal("Selected text", result.Text);
        Assert.Equal(CaptureMethod.ClipboardFirst, result.Method);
        Assert.Equal(0, h.Uia.Calls);
        Assert.Contains("clipboard", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Capture_WaitsForModifierRelease_BeforeCtrlC()
    {
        var h = new Harness();
        h.Keyboard.OnCtrlC = () => h.Clipboard.Text = "Selected text";

        await h.Service.CaptureAsync(CaptureMethod.ClipboardFirst);

        Assert.Equal(TimeSpan.FromMilliseconds(150), h.Clock.Delays[0]);
        var delayIndex = h.Events.IndexOf("delay:150");
        var ctrlIndex = h.Events.IndexOf("ctrlc");
        Assert.True(delayIndex >= 0);
        Assert.True(ctrlIndex > delayIndex);
    }

    [Fact]
    public async Task Capture_RestoreFailure_IsLogged_ButTextIsReturned()
    {
        var h = new Harness();
        h.Clipboard.ThrowOnRestore = true;
        h.Keyboard.OnCtrlC = () => h.Clipboard.Text = "Selected text";

        var result = await h.Service.CaptureAsync(CaptureMethod.ClipboardFirst);

        Assert.Equal("Selected text", result.Text);
        Assert.Equal(1, h.Clipboard.RestoreCalls);
        Assert.Contains("restore", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UiaFallback_Runs_WhenClipboardHasNoText()
    {
        var h = new Harness();
        h.Keyboard.OnCtrlC = () => { };
        h.Uia.Result = "UIA selection";

        var result = await h.Service.CaptureAsync(CaptureMethod.ClipboardFirst);

        Assert.Equal(1, h.Uia.Calls);
        Assert.Equal("UIA selection", result.Text);
        Assert.Equal(CaptureMethod.ClipboardFirst, result.Method);
        Assert.Contains("uia", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TimeSpan.FromMilliseconds(500), h.Uia.LastTimeout);
    }

    [Fact]
    public async Task ClipboardOnly_NeverCallsUia()
    {
        var h = new Harness();
        h.Keyboard.OnCtrlC = () => { };

        var result = await h.Service.CaptureAsync(CaptureMethod.ClipboardOnly);

        Assert.Equal(0, h.Uia.Calls);
        Assert.True(string.IsNullOrWhiteSpace(result.Text));
    }

    [Fact]
    public async Task UiaFirst_TriesUiaBeforeClipboard()
    {
        var h = new Harness();
        h.Uia.Result = "UIA selection";

        var result = await h.Service.CaptureAsync(CaptureMethod.UiaFirst);

        Assert.Equal(1, h.Uia.Calls);
        Assert.Equal(0, h.Clipboard.SetCalls);
        Assert.Equal(0, h.Keyboard.SendCalls);
        Assert.Equal("UIA selection", result.Text);
        Assert.Equal(CaptureMethod.UiaFirst, result.Method);
        Assert.DoesNotContain("set", h.Events);
    }

    [Fact]
    public async Task UiaOnly_NeverTouchesClipboard()
    {
        var h = new Harness();
        h.Uia.Result = "UIA selection";

        var result = await h.Service.CaptureAsync(CaptureMethod.UiaOnly);

        Assert.Equal(0, h.Clipboard.SnapshotCalls);
        Assert.Equal(0, h.Clipboard.SetCalls);
        Assert.Equal(0, h.Keyboard.SendCalls);
        Assert.Equal(1, h.Uia.Calls);
        Assert.Equal("UIA selection", result.Text);
    }

    [Fact]
    public async Task TotalFailure_ReturnsDiagnostic()
    {
        var h = new Harness();
        h.Keyboard.OnCtrlC = () => { };

        var result = await h.Service.CaptureAsync(CaptureMethod.ClipboardFirst);

        Assert.True(string.IsNullOrWhiteSpace(result.Text));
        Assert.Contains("clipboard unchanged; UIA no selection", result.Diagnostic, StringComparison.Ordinal);
    }

    private sealed class Harness
    {
        public List<string> Events { get; } = new();

        public FakeClipboard Clipboard { get; }

        public FakeKeyboardSender Keyboard { get; }

        public FakeUiaSelectionReader Uia { get; }

        public FakeClock Clock { get; }

        public SelectionService Service { get; }

        public Harness()
        {
            Clipboard = new FakeClipboard(Events);
            Keyboard = new FakeKeyboardSender(Events);
            Uia = new FakeUiaSelectionReader(Events);
            Clock = new FakeClock(Events);
            Service = new SelectionService(Clipboard, Keyboard, Uia, Clock);
        }
    }

    private sealed class FakeClipboard : IClipboard
    {
        private readonly List<string> _events;

        public FakeClipboard(List<string> events)
        {
            _events = events;
            Text = "previous clipboard";
        }

        public string? Text { get; set; }

        public int SnapshotCalls { get; private set; }

        public int SetCalls { get; private set; }

        public int RestoreCalls { get; private set; }

        public bool ThrowOnRestore { get; set; }

        public Dictionary<int, object?> Snapshot()
        {
            SnapshotCalls++;
            _events.Add("snapshot");
            return new Dictionary<int, object?> { [1] = Text };
        }

        public void Restore(Dictionary<int, object?> snapshot)
        {
            RestoreCalls++;
            _events.Add("restore");

            if (ThrowOnRestore)
            {
                throw new InvalidOperationException("format 1 failed");
            }

            if (snapshot.TryGetValue(1, out var value))
            {
                Text = value as string;
            }
        }

        public string? GetText() => Text;

        public void SetText(string text)
        {
            SetCalls++;
            _events.Add("set");
            Text = text;
        }
    }

    private sealed class FakeKeyboardSender : IKeyboardSender
    {
        private readonly List<string> _events;

        public FakeKeyboardSender(List<string> events)
        {
            _events = events;
        }

        public Action? OnCtrlC { get; set; }

        public int SendCalls { get; private set; }

        public void SendCtrlC()
        {
            SendCalls++;
            _events.Add("ctrlc");
            OnCtrlC?.Invoke();
        }
    }

    private sealed class FakeUiaSelectionReader : IUiaSelectionReader
    {
        private readonly List<string> _events;

        public FakeUiaSelectionReader(List<string> events)
        {
            _events = events;
        }

        public string? Result { get; set; }

        public int Calls { get; private set; }

        public TimeSpan LastTimeout { get; private set; }

        public Task<string?> TryReadSelectionAsync(TimeSpan timeout)
        {
            Calls++;
            LastTimeout = timeout;
            _events.Add("uia");
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeClock : IClock
    {
        private readonly List<string> _events;

        public FakeClock(List<string> events)
        {
            _events = events;
        }

        public List<TimeSpan> Delays { get; } = new();

        public Task Delay(TimeSpan delay)
        {
            Delays.Add(delay);
            _events.Add($"delay:{(int)delay.TotalMilliseconds}");
            return Task.CompletedTask;
        }
    }
}
