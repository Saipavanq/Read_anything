using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Input;
using ReadAloud.Models;
using ReadAloud.Services;
using ReadAloud.ViewModels;

namespace ReadAloud.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "readaloud-settings-tests-" + Guid.NewGuid().ToString("N"));

    public SettingsTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void ThemeToggle_SavesAndAppliesOrbit()
    {
        var h = new Harness(_root);

        h.ViewModel.Theme = "orbit";
        h.ViewModel.Save();

        Assert.Equal("orbit", h.Config.Load().Theme);
        Assert.Equal("orbit", h.Themes.CurrentTheme);
        Assert.Equal("orbit", h.Main.Theme);
    }

    [Fact]
    public void ThemeToggle_AppliesAndPersistsImmediately()
    {
        var h = new Harness(_root);

        h.ViewModel.Theme = "orbit";

        Assert.Equal("orbit", h.Config.Load().Theme);
        Assert.Equal("orbit", h.Themes.CurrentTheme);
        Assert.Equal("orbit", h.Main.Theme);
    }

    [Fact]
    public void ColorModeToggle_PersistsAndAppliesWithSelectedTheme()
    {
        var h = new Harness(_root);

        h.ViewModel.ColorMode = "dark";
        h.ViewModel.Theme = "orbit";

        Assert.Equal("dark", h.Config.Load().ColorMode);
        Assert.Equal("orbit", h.Config.Load().Theme);
        Assert.Equal("dark", h.Themes.CurrentMode);
        Assert.Equal("orbit", h.Themes.CurrentTheme);
        Assert.Equal("dark", h.Main.ColorMode);
        Assert.Equal("orbit", h.Main.Theme);
    }

    [Fact]
    public void ColorMode_AppliesImmediatelyWithoutSave()
    {
        var h = new Harness(_root);

        h.ViewModel.ColorMode = "light";

        Assert.Equal("light", h.Config.Load().ColorMode);
        Assert.Equal("light", h.Themes.CurrentMode);
        Assert.Equal("light", h.Main.ColorMode);
    }

    [Fact]
    public void FinishRecording_UpdatesReadHotkey()
    {
        var h = new Harness(_root);

        h.ViewModel.BeginRecording("read");
        Assert.Equal(1, h.Registrar.UnregisterCount);

        h.ViewModel.FinishRecording("read", ModifierKeys.Control | ModifierKeys.Shift, Key.T);
        h.ViewModel.Save();

        Assert.Equal("Ctrl+Shift+T", h.Config.Load().HotkeyRead);
    }

    [Fact]
    public void Save_InvalidHotkey_ShowsValidationAndKeepsConfig()
    {
        var h = new Harness(_root);

        h.ViewModel.HotkeyRead = "Ctrl+Alt";
        h.ViewModel.Save();

        Assert.False(string.IsNullOrWhiteSpace(h.ViewModel.ValidationMessage));
        Assert.Equal("Ctrl+Alt+R", h.Config.Load().HotkeyRead);
    }

    [Fact]
    public void DeferredVoiceRow_IsNotEditable()
    {
        var h = new Harness(_root);

        Assert.False(h.ViewModel.IsVoiceCommandEditable);
    }

    [Fact]
    public void PiperSettings_LoadIntoViewModel()
    {
        var appData = Path.Combine(_root, "appdata");
        Directory.CreateDirectory(appData);
        File.WriteAllText(Path.Combine(appData, "settings.json"), """
            {
              "tts_engine": "sapi",
              "piper_python": "C:\\tools\\python.exe",
              "piper_model_dir": "C:\\voices",
              "piper_model": "en_US-test-medium.onnx",
              "piper_speaker": 7,
              "piper_length_scale": 1.25
            }
            """);

        var h = new Harness(_root);

        Assert.Equal("sapi", h.ViewModel.TtsEngine);
        Assert.Equal(@"C:\tools\python.exe", h.ViewModel.PiperPython);
        Assert.Equal(@"C:\voices", h.ViewModel.PiperModelDir);
        Assert.Equal("en_US-test-medium.onnx", h.ViewModel.PiperModel);
        Assert.Equal(7, h.ViewModel.PiperSpeaker);
        Assert.Equal(1.25, h.ViewModel.PiperLengthScale);
        Assert.Equal(new[] { "sapi", "piper" }, h.ViewModel.SpeechEngines);
    }

    [Fact]
    public void PiperSettings_SaveBackToConfig()
    {
        var h = new Harness(_root);

        h.ViewModel.TtsEngine = "sapi";
        h.ViewModel.PiperPython = @"C:\venv\python.exe";
        h.ViewModel.PiperModelDir = @"C:\models";
        h.ViewModel.PiperModel = "en_US-amy-medium.onnx";
        h.ViewModel.PiperSpeaker = 9;
        h.ViewModel.PiperLengthScale = 0.75;
        h.ViewModel.Save();

        var saved = h.Config.Load();
        Assert.Equal("sapi", saved.TtsEngine);
        Assert.Equal(@"C:\venv\python.exe", saved.PiperPython);
        Assert.Equal(@"C:\models", saved.PiperModelDir);
        Assert.Equal("en_US-amy-medium.onnx", saved.PiperModel);
        Assert.Equal(9, saved.PiperSpeaker);
        Assert.Equal(0.75, saved.PiperLengthScale);
    }

    private sealed class Harness
    {
        public Harness(string root)
        {
            Config = new ConfigService(Path.Combine(root, "appdata", "settings.json"), Path.Combine(root, "legacy", "settings.json"));
            Registrar = new FakeHotkeyRegistrar();
            Hotkeys = new HotkeyService(Registrar);
            Themes = new FakeThemeService();
            Main = new MainViewModel(Themes, Registrar);
            ViewModel = new SettingsViewModel(Config, Hotkeys, Themes, Main);
        }

        public ConfigService Config { get; }

        public FakeHotkeyRegistrar Registrar { get; }

        public HotkeyService Hotkeys { get; }

        public FakeThemeService Themes { get; }

        public MainViewModel Main { get; }

        public SettingsViewModel ViewModel { get; }
    }

    private sealed class FakeThemeService : IThemeService
    {
        public string CurrentTheme { get; private set; } = "aurora";

        public string CurrentMode { get; private set; } = "system";

        public void Apply(string mode, string theme)
        {
            CurrentMode = mode;
            CurrentTheme = theme;
        }
    }

    private sealed class FakeHotkeyRegistrar : IHotkeyRegistrar
    {
        public event Action<string>? HotkeyPressed;

        public List<HotkeyBinding> Registrations { get; } = new();

        public int UnregisterCount { get; private set; }

        public bool TryRegister(HotkeyBinding binding)
        {
            Registrations.Add(binding);
            return true;
        }

        public void UnregisterAll()
        {
            UnregisterCount++;
            Registrations.Clear();
        }
    }
}
