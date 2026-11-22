namespace ReadAloud.Models;

public sealed class AppConfig
{
    public int SchemaVersion { get; set; } = 1;
    public string Theme { get; set; } = "aurora";
    public string ColorMode { get; set; } = "system";
    public string HotkeyRead { get; set; } = "Ctrl+Alt+R";
    public string HotkeyVoice { get; set; } = "Ctrl+Alt+V";
    public string HotkeyStop { get; set; } = "Ctrl+Alt+X";
    public CaptureMethod CaptureMethod { get; set; } = CaptureMethod.ClipboardFirst;
    public string OfflineVoice { get; set; } = "";
    public int SapiRate { get; set; }
    public int Volume { get; set; } = 100;
    public int MaxChars { get; set; } = 4000;
    public bool ShowActivity { get; set; } = true;
    public double PlaybackSpeed { get; set; } = 1.0;
    public bool AutoReadSelection { get; set; }
    public bool MiniPlayerEnabled { get; set; } = true;
    public bool MiniPlayerAlwaysOnTop { get; set; }
    public bool MiniPlayerAutoHide { get; set; } = true;
    public bool HistoryEnabled { get; set; }
    public int HistoryRetentionDays { get; set; } = 30;
    public bool StartWithWindows { get; set; }
    public bool MinimizeToTray { get; set; }
    public bool ClipboardRestore { get; set; } = true;
}
