using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ReadAloud.Models;

namespace ReadAloud.Services;

public sealed class ConfigService
{
    private static readonly HashSet<string> KnownKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "schemaVersion", "theme", "color_mode", "hotkey_read", "hotkey_voice", "hotkey_stop",
        "capture_method", "offline_voice", "sapi_rate", "volume", "max_chars",
        "show_activity", "playback_speed", "auto_read_selection", "mini_player_enabled",
        "mini_player_always_on_top", "mini_player_auto_hide", "history_enabled",
        "history_retention_days", "start_with_windows", "minimize_to_tray", "clipboard_restore",
        "tts_engine", "piper_python", "piper_model_dir", "piper_model", "piper_speaker",
        "piper_length_scale"
    };

    private readonly string _appDataConfigPath;
    private readonly string _legacyConfigPath;
    private Dictionary<string, JsonElement> _unknown = new(StringComparer.OrdinalIgnoreCase);

    public ConfigService(string appDataConfigPath, string legacyConfigPath)
    {
        _appDataConfigPath = appDataConfigPath;
        _legacyConfigPath = legacyConfigPath;
    }

    public AppConfig Load()
    {
        var config = new AppConfig();
        _unknown = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        if (TryRead(_appDataConfigPath, out var root))
        {
            Apply(root, config);
        }
        else if (TryRead(_legacyConfigPath, out root))
        {
            Apply(root, config);
        }
        else
        {
            return config;
        }

        Normalize(config);
        return config;
    }

    public void Save(AppConfig config)
    {
        var directory = Path.GetDirectoryName(_appDataConfigPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(_appDataConfigPath);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();

        foreach (var pair in _unknown)
        {
            if (KnownKeys.Contains(pair.Key))
            {
                continue;
            }

            writer.WritePropertyName(pair.Key);
            pair.Value.WriteTo(writer);
        }

        writer.WriteNumber("schemaVersion", config.SchemaVersion);
        writer.WriteString("theme", config.Theme);
        writer.WriteString("color_mode", config.ColorMode);
        writer.WriteString("hotkey_read", config.HotkeyRead);
        writer.WriteString("hotkey_voice", config.HotkeyVoice);
        writer.WriteString("hotkey_stop", config.HotkeyStop);
        writer.WriteString("capture_method", config.CaptureMethod.ToString());
        writer.WriteString("offline_voice", config.OfflineVoice);
        writer.WriteNumber("sapi_rate", config.SapiRate);
        writer.WriteNumber("volume", config.Volume);
        writer.WriteNumber("max_chars", config.MaxChars);
        writer.WriteBoolean("show_activity", config.ShowActivity);
        writer.WriteNumber("playback_speed", config.PlaybackSpeed);
        writer.WriteBoolean("auto_read_selection", config.AutoReadSelection);
        writer.WriteBoolean("mini_player_enabled", config.MiniPlayerEnabled);
        writer.WriteBoolean("mini_player_always_on_top", config.MiniPlayerAlwaysOnTop);
        writer.WriteBoolean("mini_player_auto_hide", config.MiniPlayerAutoHide);
        writer.WriteBoolean("history_enabled", config.HistoryEnabled);
        writer.WriteNumber("history_retention_days", config.HistoryRetentionDays);
        writer.WriteBoolean("start_with_windows", config.StartWithWindows);
        writer.WriteBoolean("minimize_to_tray", config.MinimizeToTray);
        writer.WriteBoolean("clipboard_restore", config.ClipboardRestore);
        writer.WriteString("tts_engine", config.TtsEngine);
        writer.WriteString("piper_python", config.PiperPython);
        writer.WriteString("piper_model_dir", config.PiperModelDir);
        writer.WriteString("piper_model", config.PiperModel);
        writer.WriteNumber("piper_speaker", config.PiperSpeaker);
        writer.WriteNumber("piper_length_scale", config.PiperLengthScale);

        writer.WriteEndObject();
    }

    private bool TryRead(string path, out JsonElement root)
    {
        root = default;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                _unknown[property.Name] = property.Value.Clone();
            }

            root = document.RootElement.Clone();
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // A corrupt or unreadable file must never block startup.
            return false;
        }
    }

    private static void Apply(JsonElement root, AppConfig config)
    {
        config.Theme = GetString(root, "theme", config.Theme);
        config.ColorMode = GetString(root, "color_mode", config.ColorMode);
        config.HotkeyRead = GetString(root, "hotkey_read", config.HotkeyRead);
        config.HotkeyVoice = GetString(root, "hotkey_voice", config.HotkeyVoice);
        config.HotkeyStop = GetString(root, "hotkey_stop", config.HotkeyStop);
        config.OfflineVoice = GetString(root, "offline_voice", config.OfflineVoice);
        config.CaptureMethod = ParseCaptureMethod(GetString(root, "capture_method", "ClipboardFirst"));
        config.SapiRate = GetInt(root, "sapi_rate", config.SapiRate);
        config.Volume = GetInt(root, "volume", config.Volume);
        config.MaxChars = GetInt(root, "max_chars", config.MaxChars);
        config.ShowActivity = GetBool(root, "show_activity", config.ShowActivity);
        config.AutoReadSelection = GetBool(root, "auto_read_selection", config.AutoReadSelection);
        config.MiniPlayerEnabled = GetBool(root, "mini_player_enabled", config.MiniPlayerEnabled);
        config.MiniPlayerAlwaysOnTop = GetBool(root, "mini_player_always_on_top", config.MiniPlayerAlwaysOnTop);
        config.MiniPlayerAutoHide = GetBool(root, "mini_player_auto_hide", config.MiniPlayerAutoHide);
        config.HistoryEnabled = GetBool(root, "history_enabled", config.HistoryEnabled);
        config.HistoryRetentionDays = GetInt(root, "history_retention_days", config.HistoryRetentionDays);
        config.StartWithWindows = GetBool(root, "start_with_windows", config.StartWithWindows);
        config.MinimizeToTray = GetBool(root, "minimize_to_tray", config.MinimizeToTray);
        config.ClipboardRestore = GetBool(root, "clipboard_restore", config.ClipboardRestore);
        config.TtsEngine = GetString(root, "tts_engine", config.TtsEngine);
        config.PiperPython = GetString(root, "piper_python", config.PiperPython);
        config.PiperModelDir = GetString(root, "piper_model_dir", config.PiperModelDir);
        config.PiperModel = GetString(root, "piper_model", config.PiperModel);
        config.PiperSpeaker = GetInt(root, "piper_speaker", config.PiperSpeaker);

        if (TryGet(root, "piper_length_scale", out var lengthScale) && lengthScale.TryGetDouble(out var parsedLengthScale))
        {
            config.PiperLengthScale = parsedLengthScale;
        }

        if (TryGet(root, "playback_speed", out var speed) && speed.TryGetDouble(out var parsedSpeed))
        {
            config.PlaybackSpeed = parsedSpeed;
        }
        else
        {
            config.PlaybackSpeed = Math.Clamp(Math.Pow(2, config.SapiRate / 10.0), 0.5, 3.0);
        }
    }

    private static void Normalize(AppConfig config)
    {
        config.SchemaVersion = 1;
        if (config.Theme is not ("aurora" or "orbit"))
        {
            config.Theme = "aurora";
        }

        var colorMode = config.ColorMode.ToLowerInvariant();
        config.ColorMode = colorMode is "system" or "dark" or "light" ? colorMode : "system";

        config.HotkeyRead = ValidHotkey(config.HotkeyRead, "Ctrl+Alt+R");
        config.HotkeyVoice = ValidHotkey(config.HotkeyVoice, "Ctrl+Alt+V");
        config.HotkeyStop = ValidHotkey(config.HotkeyStop, "Ctrl+Alt+X");
        config.Volume = Math.Clamp(config.Volume, 0, 100);
        config.SapiRate = Math.Clamp(config.SapiRate, -10, 10);
        config.MaxChars = Math.Clamp(config.MaxChars, 1, 1_000_000);
        config.HistoryRetentionDays = Math.Clamp(config.HistoryRetentionDays, 1, 365);
        config.PlaybackSpeed = Math.Clamp(config.PlaybackSpeed, 0.5, 3.0);

        var ttsEngine = config.TtsEngine.ToLowerInvariant();
        config.TtsEngine = ttsEngine is "sapi" or "piper" ? ttsEngine : "piper";
        config.PiperSpeaker = Math.Clamp(config.PiperSpeaker, 0, 32);
        config.PiperLengthScale = Math.Clamp(config.PiperLengthScale, 0.1, 10);
    }

    private static string ValidHotkey(string value, string fallback)
        => !string.IsNullOrWhiteSpace(value) && value.Contains('+') && !value.EndsWith('+')
            ? value
            : fallback;

    private static CaptureMethod ParseCaptureMethod(string value) => value.ToLowerInvariant() switch
    {
        "auto" or "clipboardfirst" => CaptureMethod.ClipboardFirst,
        "clipboardonly" => CaptureMethod.ClipboardOnly,
        "uiafirst" => CaptureMethod.UiaFirst,
        "uiaonly" => CaptureMethod.UiaOnly,
        _ => CaptureMethod.ClipboardFirst
    };

    private static bool TryGet(JsonElement root, string name, out JsonElement value)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string GetString(JsonElement root, string name, string fallback)
        => TryGet(root, name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;

    private static int GetInt(JsonElement root, string name, int fallback)
        => TryGet(root, name, out var value) && value.TryGetInt32(out var parsed)
            ? parsed
            : fallback;

    private static bool GetBool(JsonElement root, string name, bool fallback)
        => TryGet(root, name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;
}
