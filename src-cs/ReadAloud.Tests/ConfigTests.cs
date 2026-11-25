using System;
using System.IO;
using System.Text.Json;
using ReadAloud.Models;
using ReadAloud.Services;

namespace ReadAloud.Tests;

public sealed class ConfigTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "readaloud-config-tests-" + Guid.NewGuid().ToString("N"));

    public ConfigTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    private string AppDataPath => Path.Combine(_root, "appdata", "settings.json");
    private string LegacyPath => Path.Combine(_root, "legacy", "config", "settings.json");

    private void Write(string path, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    private ConfigService Service() => new(AppDataPath, LegacyPath);

    [Fact]
    public void LegacyKeys_MigrateFromInstalledConfig()
    {
        Write(LegacyPath, """
        {
          "theme": "orbit",
          "hotkey_read": "Ctrl+Alt+T",
          "capture_method": "auto",
          "volume": 200
        }
        """);

        var config = Service().Load();

        Assert.Equal("orbit", config.Theme);
        Assert.Equal("Ctrl+Alt+T", config.HotkeyRead);
        Assert.Equal(CaptureMethod.ClipboardFirst, config.CaptureMethod);
        Assert.Equal(100, config.Volume);
        Assert.Equal(1, config.SchemaVersion);
    }

    [Fact]
    public void AutoCapture_MigratesToClipboardFirst()
    {
        Write(LegacyPath, """{"capture_method": "auto"}""");

        Assert.Equal(CaptureMethod.ClipboardFirst, Service().Load().CaptureMethod);
    }

    [Fact]
    public void CorruptLegacyConfig_UsesDefaults()
    {
        Write(LegacyPath, "{ this is not json");

        var config = Service().Load();

        Assert.Equal("aurora", config.Theme);
        Assert.Equal("Ctrl+Alt+R", config.HotkeyRead);
        Assert.Equal(CaptureMethod.ClipboardFirst, config.CaptureMethod);
        Assert.Equal(100, config.Volume);
        Assert.Equal(1, config.SchemaVersion);
    }

    [Fact]
    public void CSharpConfigWins_WhenBothFilesExist()
    {
        Write(AppDataPath, """{"theme": "orbit"}""");
        Write(LegacyPath, """{"theme": "aurora"}""");

        Assert.Equal("orbit", Service().Load().Theme);
    }

    [Fact]
    public void Save_WritesSchemaVersionOne()
    {
        var service = Service();
        var config = service.Load();
        config.Theme = "orbit";

        service.Save(config);

        using var doc = JsonDocument.Parse(File.ReadAllText(AppDataPath));
        Assert.Equal(1, doc.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("orbit", doc.RootElement.GetProperty("theme").GetString());
    }

    [Fact]
    public void LegacySapiRate_DerivesPlaybackSpeedOnce()
    {
        Write(LegacyPath, """{"sapi_rate": -10}""");
        Assert.Equal(0.5, Service().Load().PlaybackSpeed, 3);

        var secondLegacy = Path.Combine(_root, "legacy2", "settings.json");
        Write(secondLegacy, """{"sapi_rate": 10}""");
        var second = new ConfigService(Path.Combine(_root, "appdata2", "settings.json"), secondLegacy);
        Assert.Equal(2.0, second.Load().PlaybackSpeed, 3);
    }

    [Fact]
    public void PlaybackSpeed_Wins_WhenBothPresent()
    {
        Write(AppDataPath, """{"sapi_rate": -10, "playback_speed": 1.5}""");

        Assert.Equal(1.5, Service().Load().PlaybackSpeed, 3);
    }

    [Fact]
    public void OfflineVoice_MatchesDescriptionThenName()
    {
        var voice = new VoiceInfo("Microsoft Zira Desktop", "Microsoft Zira Desktop - English (United States)", "en-US", "Female", false);

        Assert.True(VoiceIdentity.Matches("microsoft zira desktop - english (united states)", voice));
        Assert.True(VoiceIdentity.Matches("Microsoft Zira Desktop", voice));
        Assert.False(VoiceIdentity.Matches("Microsoft David Desktop", voice));
    }

    [Fact]
    public void BadNumbers_AreClamped()
    {
        Write(LegacyPath, """
        {
          "volume": 200,
          "sapi_rate": 20,
          "max_chars": 0,
          "history_retention_days": 500,
          "playback_speed": 9
        }
        """);

        var config = Service().Load();

        Assert.Equal(100, config.Volume);
        Assert.Equal(10, config.SapiRate);
        Assert.Equal(1, config.MaxChars);
        Assert.Equal(365, config.HistoryRetentionDays);
        Assert.Equal(3.0, config.PlaybackSpeed, 3);
    }

    [Theory]
    [InlineData("dark", "dark")]
    [InlineData("light", "light")]
    [InlineData("system", "system")]
    [InlineData("orbit", "system")]
    public void ColorMode_NormalizesToSystem(string input, string expected)
    {
        Write(AppDataPath, $"{{\"color_mode\":\"{input}\",\"theme\":\"orbit\"}}");

        var config = Service().Load();

        Assert.Equal(expected, config.ColorMode);
        Assert.Equal("orbit", config.Theme);
    }

    [Fact]
    public void ColorMode_Missing_DefaultsToSystem()
    {
        Write(AppDataPath, """{"theme":"aurora"}""");

        var config = Service().Load();

        Assert.Equal("system", config.ColorMode);
        Assert.Equal("aurora", config.Theme);
    }

    [Fact]
    public void Save_WritesColorMode()
    {
        var service = Service();
        var config = service.Load();
        config.ColorMode = "dark";

        service.Save(config);

        using var doc = JsonDocument.Parse(File.ReadAllText(AppDataPath));
        Assert.Equal("dark", doc.RootElement.GetProperty("color_mode").GetString());
    }

    [Fact]
    public void PiperDefaults_AreApplied()
    {
        var config = new AppConfig();

        Assert.Equal("piper", config.TtsEngine);
        Assert.Equal("", config.PiperPython);
        Assert.Equal("", config.PiperModelDir);
        Assert.Equal("", config.PiperModel);
        Assert.Equal(0, config.PiperSpeaker);
        Assert.Equal(1.0, config.PiperLengthScale, 3);
    }

    [Fact]
    public void PiperKeys_RoundTripThroughConfigService()
    {
        var service = Service();
        var config = service.Load();
        config.TtsEngine = "sapi";
        config.PiperPython = "C:\\venv\\Scripts\\python.exe";
        config.PiperModelDir = "C:\\models";
        config.PiperModel = "en_US-amy-medium.onnx";
        config.PiperSpeaker = 3;
        config.PiperLengthScale = 1.25;

        service.Save(config);

        var reloaded = new ConfigService(AppDataPath, LegacyPath).Load();
        Assert.Equal("sapi", reloaded.TtsEngine);
        Assert.Equal("C:\\venv\\Scripts\\python.exe", reloaded.PiperPython);
        Assert.Equal("C:\\models", reloaded.PiperModelDir);
        Assert.Equal("en_US-amy-medium.onnx", reloaded.PiperModel);
        Assert.Equal(3, reloaded.PiperSpeaker);
        Assert.Equal(1.25, reloaded.PiperLengthScale, 3);
    }

    [Fact]
    public void UnknownPythonKeys_SurviveCSharpSave()
    {
        Write(LegacyPath, """
        {
          "tts_engine": "piper",
          "whisper_model": "base.en"
        }
        """);

        var service = Service();
        service.Save(service.Load());

        using var doc = JsonDocument.Parse(File.ReadAllText(AppDataPath));
        Assert.Equal("piper", doc.RootElement.GetProperty("tts_engine").GetString());
        Assert.Equal("base.en", doc.RootElement.GetProperty("whisper_model").GetString());
    }

    [Fact]
    public void BadPiperNumbers_AreClamped()
    {
        Write(AppDataPath, """
        {
          "tts_engine": "bogus",
          "piper_speaker": -5,
          "piper_length_scale": 99
        }
        """);

        var config = Service().Load();

        Assert.Equal("piper", config.TtsEngine);
        Assert.Equal(0, config.PiperSpeaker);
        Assert.Equal(10, config.PiperLengthScale, 3);
    }
}
