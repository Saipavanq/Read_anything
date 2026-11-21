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

        public void Apply(string theme) => CurrentTheme = theme == "orbit" ? "orbit" : "aurora";
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
