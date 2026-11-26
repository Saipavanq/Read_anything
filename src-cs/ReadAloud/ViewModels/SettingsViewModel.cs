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
    private readonly StartupService? _startup;
    private readonly Action<Action>? _dispatcher;
    private string? _recordingAction;

    private string _colorMode = "system";
    private string _theme = "aurora";
    private string _hotkeyRead = "Ctrl+Alt+R";
    private string _hotkeyVoice = "Ctrl+Alt+V";
    private string _hotkeyStop = "Ctrl+Alt+X";
    private CaptureMethod _captureMethod = CaptureMethod.ClipboardFirst;
    private string _offlineVoice = string.Empty;
    private int _sapiRate;
    private string _ttsEngine = "piper";
    private string _edgeVoicePriorityText = string.Empty;
    private string _piperPython = string.Empty;
    private string _piperModelDir = string.Empty;
    private string _piperModel = string.Empty;
    private int _piperSpeaker;
    private double _piperLengthScale = 1.0;
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
        MainViewModel main,
        StartupService? startup = null,
        Action<Action>? dispatcher = null)
    {
        _config = config;
        _hotkeys = hotkeys;
        _main = main;
        _startup = startup;
        _dispatcher = dispatcher;

        var current = config.Load();
        _colorMode = current.ColorMode;
        _theme = current.Theme;
        _hotkeyRead = current.HotkeyRead;
        _hotkeyVoice = current.HotkeyVoice;
        _hotkeyStop = current.HotkeyStop;
        _captureMethod = current.CaptureMethod;
        _offlineVoice = current.OfflineVoice;
        _sapiRate = current.SapiRate;
        _ttsEngine = current.TtsEngine;
        _edgeVoicePriorityText = string.Join(", ", current.EdgeVoicePriority);
        _piperPython = current.PiperPython;
        _piperModelDir = current.PiperModelDir;
        _piperModel = current.PiperModel;
        _piperSpeaker = current.PiperSpeaker;
        _piperLengthScale = current.PiperLengthScale;
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

    public string ColorMode
    {
        get => _colorMode;
        set
        {
            if (!SetProperty(ref _colorMode, value))
            {
                return;
            }

            // The color mode decision wins immediately: persist it now and apply
            // it on the UI dispatcher, not only when the Settings dialog is saved.
            var config = _config.Load();
            config.ColorMode = value;
            _config.Save(config);

            void Apply()
            {
                _main.ApplyTheme(ColorMode, Theme);
            }

            if (_dispatcher is null)
            {
                Apply();
            }
            else
            {
                _dispatcher(Apply);
            }
        }
    }

    public string Theme
    {
        get => _theme;
        set
        {
            if (!SetProperty(ref _theme, value))
            {
                return;
            }

            // The theme decision wins immediately: persist it now and apply it
            // on the UI dispatcher, not only when the Settings dialog is saved.
            var config = _config.Load();
            config.Theme = value;
            _config.Save(config);

            void Apply()
            {
                _main.ApplyTheme(ColorMode, value);
            }

            if (_dispatcher is null)
            {
                Apply();
            }
            else
            {
                _dispatcher(Apply);
            }
        }
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

    public IReadOnlyList<string> SpeechEngines { get; } = new[] { "sapi", "piper", "edge" };

    public string TtsEngine
    {
        get => _ttsEngine;
        set => SetProperty(ref _ttsEngine, value);
    }

    public string EdgeVoicePriorityText
    {
        get => _edgeVoicePriorityText;
        set => SetProperty(ref _edgeVoicePriorityText, value);
    }

    public string PiperPython
    {
        get => _piperPython;
        set => SetProperty(ref _piperPython, value);
    }

    public string PiperModelDir
    {
        get => _piperModelDir;
        set => SetProperty(ref _piperModelDir, value);
    }

    public string PiperModel
    {
        get => _piperModel;
        set => SetProperty(ref _piperModel, value);
    }

    public int PiperSpeaker
    {
        get => _piperSpeaker;
        set => SetProperty(ref _piperSpeaker, value);
    }

    public double PiperLengthScale
    {
        get => _piperLengthScale;
        set => SetProperty(ref _piperLengthScale, value);
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

    public string PrivacyDisclosure { get; } =
        "Read Aloud is offline-first and sends no text or telemetry to any service. "
        + "Auto-read observes only mouse release events while enabled; it records no keystrokes or mouse coordinates. "
        + "Local history is stored only on this device and can be disabled or cleared at any time.";

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
        ApplyStartupSettings();
        _main.ApplyTheme(config.ColorMode, config.Theme);

        var conflicts = _hotkeys.RegisterAll(config, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        ValidationMessage = conflicts.Count == 0 ? "Settings saved." : string.Join(" ", conflicts);
    }

    public void ApplyStartupSettings() => _startup?.SetStartWithWindows(StartWithWindows);

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
        config.ColorMode = ColorMode;
        config.Theme = Theme;
        config.HotkeyRead = HotkeyRead;
        config.HotkeyVoice = HotkeyVoice;
        config.HotkeyStop = HotkeyStop;
        config.CaptureMethod = CaptureMethod;
        config.OfflineVoice = OfflineVoice;
        config.SapiRate = SapiRate;
        config.TtsEngine = TtsEngine;
        config.EdgeVoicePriority = new List<string>(
            EdgeVoicePriorityText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        config.PiperPython = PiperPython;
        config.PiperModelDir = PiperModelDir;
        config.PiperModel = PiperModel;
        config.PiperSpeaker = PiperSpeaker;
        config.PiperLengthScale = PiperLengthScale;
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
