using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ReadAloud.Models;
using ReadAloud.Services;
using ReadAloud.ViewModels;

namespace ReadAloud.Tests;

public sealed class VoiceLibraryTests
{
    [Fact]
    public void Refresh_MapsInstalledVoicesToCards()
    {
        var h = new Harness();
        h.Speech.Voices.Add(new VoiceInfo("Zira", "Zira description", "en-US", "Female", true));
        h.Speech.Voices.Add(new VoiceInfo("David", "David description", "en-US", "", false));

        h.ViewModel.Refresh();

        var cards = h.ViewModel.Voices;
        Assert.Equal(2, cards.Count);
        Assert.Equal("Zira", cards[0].Name);
        Assert.Equal("Zira description", cards[0].Description);
        Assert.Equal("en-US", cards[0].Language);
        Assert.Equal("Female", cards[0].Gender);
        Assert.Equal("System", cards[0].QualityLabel);
        Assert.True(cards[0].IsDefault);
        Assert.Equal("Not specified", cards[1].Gender);
        Assert.False(cards[1].IsDefault);
    }

    [Fact]
    public void FilteredVoices_FiltersBySearchAndLanguage()
    {
        var h = new Harness();
        h.Speech.Voices.Add(new VoiceInfo("Zira", "Zira description", "en-US", "Female", true));
        h.Speech.Voices.Add(new VoiceInfo("George", "George description", "en-GB", "Male", false));
        h.Speech.Voices.Add(new VoiceInfo("Jeanne", "Jeanne description", "fr-FR", "Female", false));
        h.ViewModel.Refresh();

        h.ViewModel.SearchText = "geo";

        var searched = Assert.Single(h.ViewModel.FilteredVoices);
        Assert.Equal("George", searched.Name);

        h.ViewModel.SearchText = string.Empty;
        h.ViewModel.LanguageFilter = "fr-FR";

        var filtered = Assert.Single(h.ViewModel.FilteredVoices);
        Assert.Equal("Jeanne", filtered.Name);
    }

    [Fact]
    public void Preview_UsesSelectedVoiceAndCurrentSpeed()
    {
        var h = new Harness();
        h.Speech.Voices.Add(new VoiceInfo("Zira", "Zira description", "en-US", "Female", true));
        h.ViewModel.Refresh();
        h.ApplyConfig(config =>
        {
            config.OfflineVoice = string.Empty;
            config.PlaybackSpeed = 2.0;
            config.SapiRate = -9;
            config.Volume = 80;
        });
        var card = h.ViewModel.Voices[0];

        h.ViewModel.Preview(card);

        Assert.Equal(1, h.Speech.SpeakCalls);
        Assert.False(string.IsNullOrWhiteSpace(h.Speech.LastText));
        Assert.Equal("Zira description", h.Speech.LastOptions?.VoiceName);
        Assert.Equal(10, h.Speech.LastOptions?.Rate);
        Assert.Equal(80, h.Speech.LastOptions?.Volume);
        Assert.Equal(string.Empty, h.Config.Load().OfflineVoice);
    }

    [Fact]
    public void OfflineVoice_MatchesByDescription()
    {
        var h = new Harness();
        h.Speech.Voices.Add(new VoiceInfo("Zira", "Zira description", "en-US", "Female", true));
        h.ApplyConfig(config => config.OfflineVoice = "Zira description");

        h.ViewModel.Refresh();

        Assert.Equal("Zira", h.ViewModel.SelectedVoice?.Name);
    }

    [Fact]
    public void OfflineVoice_MatchesByNameFallback()
    {
        var h = new Harness();
        h.Speech.Voices.Add(new VoiceInfo("Zira", "Zira description", "en-US", "Female", true));
        h.ApplyConfig(config => config.OfflineVoice = "Zira");

        h.ViewModel.Refresh();

        Assert.Equal("Zira", h.ViewModel.SelectedVoice?.Name);
    }

    private sealed class Harness : IDisposable
    {
        private readonly string _directory;

        public Harness()
        {
            _directory = Path.Combine(Path.GetTempPath(), "ReadAloudVoiceTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            Config = new ConfigService(
                Path.Combine(_directory, "settings.json"),
                Path.Combine(_directory, "legacy.json"));
            Speech = new FakeSpeechEngine();
            ViewModel = new VoiceLibraryViewModel(Speech, Config);
        }

        public ConfigService Config { get; }

        public FakeSpeechEngine Speech { get; }

        public VoiceLibraryViewModel ViewModel { get; }

        public void ApplyConfig(Action<AppConfig> change)
        {
            var config = Config.Load();
            change(config);
            Config.Save(config);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private sealed class FakeSpeechEngine : ISpeechEngine
    {
        private Action? _speakCompleted;
        private Action<int, int>? _speakProgress;

        public event Action? SpeakCompleted
        {
            add => _speakCompleted += value;
            remove => _speakCompleted -= value;
        }

        public event Action<int, int>? SpeakProgress
        {
            add => _speakProgress += value;
            remove => _speakProgress -= value;
        }

        public List<VoiceInfo> Voices { get; } = new();

        public bool IsSpeaking { get; private set; }

        public string? LastText { get; private set; }

        public VoiceOptions? LastOptions { get; private set; }

        public int SpeakCalls { get; private set; }

        public IReadOnlyList<VoiceInfo> GetVoices() => Voices.ToList();

        public void Speak(string text, VoiceOptions options)
        {
            SpeakCalls++;
            LastText = text;
            LastOptions = options;
        }

        public void Stop() => IsSpeaking = false;
    }
}
