using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ReadAloud.Models;
using ReadAloud.Services;
using ReadAloud.ViewModels;

namespace ReadAloud.Tests;

public sealed class HomeTests
{
    [Fact]
    public void Home_ExposesExactLabelsAndCharacterCount()
    {
        var h = new Harness();
        h.Home.TextInput = "abc";

        Assert.Equal("Listen", h.Home.PrimaryButtonText);
        Assert.Equal("Read selection", h.Home.PrimaryButtonAccessibleName);
        Assert.Equal(3, h.Home.CharacterCount);
    }

    [Fact]
    public async Task RequestListenAsync_UsesManualTextWhenPresent()
    {
        var h = new Harness();
        h.Home.TextInput = "manual text";

        await h.Home.RequestListenAsync();

        Assert.Equal("manual text", Assert.Single(h.Speech.Spoken));
        Assert.False(h.Home.IsListening);
    }

    [Fact]
    public async Task RequestReadSelectionAsync_CapturesWhenManualTextEmpty()
    {
        var h = new Harness();
        h.Keyboard.OnCtrlC = () => h.Clipboard.Text = "selected text";

        await h.Home.RequestReadSelectionAsync();

        Assert.Equal("selected text", Assert.Single(h.Speech.Spoken));
        Assert.Contains(h.Log.Entries, entry => entry.Contains("clipboard", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ClearCommand_ClearsTextAndCount()
    {
        var h = new Harness();
        h.Home.TextInput = "abc";

        h.Home.ClearCommand.Execute(null);

        Assert.Equal(string.Empty, h.Home.TextInput);
        Assert.Equal(0, h.Home.CharacterCount);
    }

    [Fact]
    public void CaptureSelector_PersistsAndHotkeySummaryRefreshes()
    {
        var h = new Harness();
        h.Config.HotkeyRead = "Ctrl+Shift+R";
        h.Home.Refresh();

        Assert.Contains("Read: Ctrl+Shift+R", h.Home.HotkeySummary);

        h.Home.SelectedCaptureMethod = CaptureMethod.UiaOnly;

        Assert.Equal(CaptureMethod.UiaOnly, h.Config.CaptureMethod);
        Assert.Equal(1, h.SaveCount);
    }

    [Fact]
    public void VoiceSelector_PersistsOfflineVoice()
    {
        var h = new Harness();

        Assert.Contains("Zira", h.Home.Voices);
        h.Home.SelectedVoice = "Zira";

        Assert.Equal("Zira", h.Config.OfflineVoice);
        Assert.Equal(1, h.SaveCount);
    }

    private sealed class Harness
    {
        public Harness()
        {
            Config = new AppConfig();
            Clipboard = new FakeClipboard();
            Keyboard = new FakeKeyboardSender();
            Speech = new FakeSpeechEngine();
            Log = new ActivityLog();
            var selection = new SelectionService(Clipboard, Keyboard, new FakeUiaSelectionReader(), new FakeClock());
            Hotkeys = new HotkeyService(new FakeHotkeyRegistrar());
            var playback = new PlaybackService(Speech, () => Config);
            var coordinator = new ReadCoordinator(selection, playback, Log, () => Config);
            Speech.Voices.Add(new VoiceInfo("Zira", "Zira", "en-US", "Female", true));
            Home = new HomeViewModel(coordinator, Log, Hotkeys, () => Config, _ => SaveCount++, Speech);
        }

        public AppConfig Config { get; }

        public FakeClipboard Clipboard { get; }

        public FakeKeyboardSender Keyboard { get; }

        public FakeSpeechEngine Speech { get; }

        public ActivityLog Log { get; }

        public HotkeyService Hotkeys { get; }

        public HomeViewModel Home { get; }

        public int SaveCount { get; private set; }
    }

    private sealed class FakeClipboard : IClipboard
    {
        public FakeClipboard()
        {
            Text = "previous clipboard";
        }

        public string? Text { get; set; }

        public Dictionary<int, object?> Snapshot()
            => new() { [1] = Text };

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

        public void SendCtrlC() => OnCtrlC?.Invoke();
    }

    private sealed class FakeUiaSelectionReader : IUiaSelectionReader
    {
        public Task<string?> TryReadSelectionAsync(TimeSpan timeout) => Task.FromResult<string?>(null);
    }

    private sealed class FakeClock : IClock
    {
        public Task Delay(TimeSpan delay) => Task.CompletedTask;
    }

    private sealed class FakeSpeechEngine : ISpeechEngine
    {
        public event Action? SpeakCompleted;

        public event Action<int, int>? SpeakProgress;

        public List<string> Spoken { get; } = new();

        public bool IsSpeaking => false;

        public List<VoiceInfo> Voices { get; } = new();

        public IReadOnlyList<VoiceInfo> GetVoices() => Voices;

        public void Speak(string text, VoiceOptions options) => Spoken.Add(text);

        public void Stop()
        {
        }
    }

    private sealed class FakeHotkeyRegistrar : IHotkeyRegistrar
    {
        public event Action<string>? HotkeyPressed;

        public bool TryRegister(HotkeyBinding binding)
        {
            HotkeyPressed?.Invoke(binding.Action);
            return true;
        }

        public void UnregisterAll()
        {
        }
    }
}
