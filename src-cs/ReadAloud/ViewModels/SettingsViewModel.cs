using System;
using System.Collections.Generic;
using System.Windows.Input;
using ReadAloud.Models;
using ReadAloud.Services;

namespace ReadAloud.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly ConfigService _config;
    private readonly HotkeyService _hotkeys;
    private readonly MainViewModel _main;
    private string? _recordingAction;

    private string _theme = "aurora";
    private string _hotkeyRead = "Ctrl+Alt+R";
    private string _hotkeyVoice = "Ctrl+Alt+V";
    private string _hotkeyStop = "Ctrl+Alt+X";
    private CaptureMethod _captureMethod = CaptureMethod.ClipboardFirst;
    private string _offlineVoice = string.Empty;
    private int _sapiRate;
    private int _volume = 100;
    private int _maxChars = 4000;
    private bool _showActivity = true;
    private double _playbackSpeed = 1.0;
    private bool _miniPlayerEnabled = true;
    private bool _miniPlayerAlwaysOnTop;
    private bool _miniPlayerAutoHide = true;
    private bool _historyEnabled;
    private int _historyRetentionDays = 30;
    private bool _autoReadSelection;
    private bool _startWithWindows;
    private bool _minimizeToTray;
    private bool _clipboardRestore = true;
    private string _validationMessage = string.Empty;

    public SettingsViewModel(
        ConfigService config,
        HotkeyService hotkeys,
        IThemeService themes,
        MainViewModel main)
    {
        _config = config;
        _hotkeys = hotkeys;
        _main = main;

        var current = config.Load();
        _theme = current.Theme;
        _hotkeyRead = current.HotkeyRead;
        _hotkeyVoice = current.HotkeyVoice;
        _hotkeyStop = current.HotkeyStop;
        _captureMethod = current.CaptureMethod;
        _offlineVoice = current.OfflineVoice;
        _sapiRate = current.SapiRate;
        _volume = current.Volume;
        _maxChars = current.MaxChars;
        _showActivity = current.ShowActivity;
        _playbackSpeed = current.PlaybackSpeed;
        _miniPlayerEnabled = current.MiniPlayerEnabled;
        _miniPlayerAlwaysOnTop = current.MiniPlayerAlwaysOnTop;
        _miniPlayerAutoHide = current.MiniPlayerAutoHide;
        _historyEnabled = current.HistoryEnabled;
        _historyRetentionDays = current.HistoryRetentionDays;
        _autoReadSelection = current.AutoReadSelection;
        _startWithWindows = current.StartWithWindows;
        _minimizeToTray = current.MinimizeToTray;
        _clipboardRestore = current.ClipboardRestore;
    }

    public string Theme
    {
        get => _theme;
        set => SetProperty(ref _theme, value);
    }

    public string HotkeyRead
    {
        get => _hotkeyRead;
        set => SetProperty(ref _hotkeyRead, value);
    }

    public string HotkeyVoice
    {
        get => _hotkeyVoice;
        set => SetProperty(ref _hotkeyVoice, value);
    }

    public string HotkeyStop
    {
        get => _hotkeyStop;
        set => SetProperty(ref _hotkeyStop, value);
    }

    public CaptureMethod CaptureMethod
    {
        get => _captureMethod;
        set => SetProperty(ref _captureMethod, value);
    }

    public string OfflineVoice
    {
        get => _offlineVoice;
        set => SetProperty(ref _offlineVoice, value);
    }

    public int SapiRate
    {
        get => _sapiRate;
        set => SetProperty(ref _sapiRate, value);
    }

    public int Volume
    {
        get => _volume;
        set => SetProperty(ref _volume, value);
    }

    public int MaxChars
    {
        get => _maxChars;
        set => SetProperty(ref _maxChars, value);
    }

    public bool ShowActivity
    {
        get => _showActivity;
        set => SetProperty(ref _showActivity, value);
    }

    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set => SetProperty(ref _playbackSpeed, value);
    }

    public bool MiniPlayerEnabled
    {
        get => _miniPlayerEnabled;
        set => SetProperty(ref _miniPlayerEnabled, value);
    }

    public bool MiniPlayerAlwaysOnTop
    {
        get => _miniPlayerAlwaysOnTop;
        set => SetProperty(ref _miniPlayerAlwaysOnTop, value);
    }

    public bool MiniPlayerAutoHide
    {
        get => _miniPlayerAutoHide;
        set => SetProperty(ref _miniPlayerAutoHide, value);
    }

    public bool HistoryEnabled
    {
        get => _historyEnabled;
        set => SetProperty(ref _historyEnabled, value);
    }

    public int HistoryRetentionDays
    {
        get => _historyRetentionDays;
        set => SetProperty(ref _historyRetentionDays, value);
    }

    public bool AutoReadSelection
    {
        get => _autoReadSelection;
        set => SetProperty(ref _autoReadSelection, value);
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetProperty(ref _startWithWindows, value);
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set => SetProperty(ref _minimizeToTray, value);
    }

    public bool ClipboardRestore
    {
        get => _clipboardRestore;
        set => SetProperty(ref _clipboardRestore, value);
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        private set => SetProperty(ref _validationMessage, value);
    }

    public bool IsVoiceCommandEditable => false;

    public void Save()
    {
        if (!_hotkeys.IsValid(HotkeyRead)
            || !_hotkeys.IsValid(HotkeyVoice)
            || !_hotkeys.IsValid(HotkeyStop))
        {
            ValidationMessage = "One or more hotkeys are invalid. Use Ctrl, Shift, Alt, or Win followed by one key.";
            return;
        }

        var config = _config.Load();
        ApplyTo(config);
        _config.Save(config);
        _main.ApplyTheme(config.Theme);

        var conflicts = _hotkeys.RegisterAll(config, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        ValidationMessage = conflicts.Count == 0 ? "Settings saved." : string.Join(" ", conflicts);
    }

    public void BeginRecording(string action)
    {
        _recordingAction = action;
        _hotkeys.BeginRecording();
        ValidationMessage = $"Press a key combination to record the {action} hotkey.";
    }

    public void FinishRecording(string action, ModifierKeys modifiers, Key key)
    {
        if (!string.Equals(_recordingAction, action, StringComparison.OrdinalIgnoreCase))
        {
            ValidationMessage = "No recorder is armed for that action.";
            return;
        }

        if (!_hotkeys.TryRecord(action, modifiers, key, out var binding, out var error))
        {
            ValidationMessage = error;
            return;
        }

        switch (action.ToLowerInvariant())
        {
            case "read":
                HotkeyRead = binding.Display;
                break;
            case "voice":
                HotkeyVoice = binding.Display;
                break;
            case "stop":
                HotkeyStop = binding.Display;
                break;
        }

        ValidationMessage = $"Recorded {binding.Display}.";
        _recordingAction = null;
        _hotkeys.EndRecording(BuildConfig());
    }

    public void CancelRecording()
    {
        _recordingAction = null;
        _hotkeys.EndRecording(_config.Load());
        ValidationMessage = "Hotkey recording cancelled.";
    }

    private AppConfig BuildConfig()
    {
        var config = _config.Load();
        ApplyTo(config);
        return config;
    }

    private void ApplyTo(AppConfig config)
    {
        config.Theme = Theme;
        config.HotkeyRead = HotkeyRead;
        config.HotkeyVoice = HotkeyVoice;
        config.HotkeyStop = HotkeyStop;
        config.CaptureMethod = CaptureMethod;
        config.OfflineVoice = OfflineVoice;
        config.SapiRate = SapiRate;
        config.Volume = Volume;
        config.MaxChars = MaxChars;
        config.ShowActivity = ShowActivity;
        config.PlaybackSpeed = PlaybackSpeed;
        config.MiniPlayerEnabled = MiniPlayerEnabled;
        config.MiniPlayerAlwaysOnTop = MiniPlayerAlwaysOnTop;
        config.MiniPlayerAutoHide = MiniPlayerAutoHide;
        config.HistoryEnabled = HistoryEnabled;
        config.HistoryRetentionDays = HistoryRetentionDays;
        config.AutoReadSelection = AutoReadSelection;
        config.StartWithWindows = StartWithWindows;
        config.MinimizeToTray = MinimizeToTray;
        config.ClipboardRestore = ClipboardRestore;
    }
}
