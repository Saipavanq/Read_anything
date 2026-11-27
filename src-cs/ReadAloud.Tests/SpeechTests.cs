using System;
using System.Collections.Generic;
using ReadAloud.Models;
using ReadAloud.Services;

namespace ReadAloud.Tests;

public sealed class SpeechTests
{
    [Fact]
    public void Speak_UsesSavedVoiceRateAndVolume()
    {
        var synth = DefaultSynth();
        var engine = new SapiSpeechEngine(synth);
        var config = new AppConfig
        {
            OfflineVoice = "Zira description",
            SapiRate = -3,
            Volume = 80
        };

        engine.Speak("Hello there.", VoiceOptions.FromConfig(config));

        Assert.Equal("Zira", synth.SelectedVoice);
        Assert.Equal(-3, synth.Rate);
        Assert.Equal(80, synth.Volume);
        Assert.Equal("Hello there.", synth.LastText);
        Assert.Equal(1, synth.SpeakCalls);
    }

    [Fact]
    public void Speak_EmptyText_DoesNothing()
    {
        var synth = DefaultSynth();
        var engine = new SapiSpeechEngine(synth);

        engine.Speak("   ", new VoiceOptions("Zira description", 0, 100));

        Assert.Equal(0, synth.SpeakCalls);
        Assert.Null(synth.LastText);
        Assert.False(engine.IsSpeaking);
    }

    [Fact]
    public void Stop_SetsIsSpeakingFalse()
    {
        var synth = DefaultSynth();
        var engine = new SapiSpeechEngine(synth);
        synth.IsSpeaking = true;

        engine.Stop();

        Assert.Equal(1, synth.StopCalls);
        Assert.False(synth.IsSpeaking);
        Assert.False(engine.IsSpeaking);
    }

    [Fact]
    public void GetVoices_MapsSapiVoices()
    {
        var synth = new FakeSynthesizer();
        synth.Voices.Add(new SyntheticVoiceDescriptor("Zira", "Zira description", "en-US", "NotSet", true));
        var engine = new SapiSpeechEngine(synth);

        var voices = engine.GetVoices();

        var voice = Assert.Single(voices);
        Assert.Equal("Zira", voice.Name);
        Assert.Equal("Zira description", voice.Description);
        Assert.Equal("en-US", voice.Language);
        Assert.Equal("Not specified", voice.Gender);
        Assert.True(voice.IsDefault);
    }

    [Fact]
    public void MissingVoice_FallsBackToDefault()
    {
        var synth = DefaultSynth();
        var engine = new SapiSpeechEngine(synth);

        engine.Speak("Hello", new VoiceOptions("missing voice", 0, 100));

        Assert.Equal("David", synth.SelectedVoice);
    }

    [Fact]
    public void SelectableEngine_DefaultConfigUsesPiper()
    {
        var sapi = new FakeSpeechEngine();
        var piper = new FakeSpeechEngine();
        var config = new AppConfig();
        var engine = new SelectableSpeechEngine(sapi, piper, () => config);

        engine.Speak("Piper default text", new VoiceOptions("voice", 0, 100));

        Assert.Equal(1, piper.SpeakCalls);
        Assert.Equal("Piper default text", piper.LastText);
        Assert.Equal(0, sapi.SpeakCalls);
        Assert.Null(sapi.LastText);
    }

    [Fact]
    public void SelectableEngine_RoutesEdge()
    {
        var sapi = new FakeSpeechEngine();
        var piper = new FakeSpeechEngine();
        var edge = new FakeSpeechEngine();
        var config = new AppConfig { TtsEngine = "edge" };
        var engine = new SelectableSpeechEngine(sapi, piper, edge, () => config);
        var options = new VoiceOptions("voice", 0, 100);
        var completed = 0;
        var progress = new List<(int Position, int Count)>();
        engine.SpeakCompleted += () => completed++;
        engine.SpeakProgress += (position, count) => progress.Add((position, count));

        engine.Speak("Edge text", options);
        edge.EmitProgress(4, 10);
        edge.EmitCompleted();
        piper.EmitCompleted();
        sapi.EmitCompleted();
        engine.Stop();

        Assert.Equal(1, edge.SpeakCalls);
        Assert.Equal("Edge text", edge.LastText);
        Assert.Equal(0, sapi.SpeakCalls);
        Assert.Null(sapi.LastText);
        Assert.Equal(0, piper.SpeakCalls);
        Assert.Null(piper.LastText);
        Assert.Equal(1, completed);
        Assert.Equal((4, 10), Assert.Single(progress));
        Assert.Equal(1, edge.StopCalls);
        Assert.Equal(0, sapi.StopCalls);
        Assert.Equal(0, piper.StopCalls);
    }

    private static FakeSynthesizer DefaultSynth()
    {
        var synth = new FakeSynthesizer();
        synth.Voices.Add(new SyntheticVoiceDescriptor("Zira", "Zira description", "en-US", "Female", false));
        synth.Voices.Add(new SyntheticVoiceDescriptor("David", "David description", "en-US", "Male", true));
        return synth;
    }

    private sealed class FakeSynthesizer : ISpeechSynthesizer
    {
        public event Action? SpeakCompleted;

        public event Action<int, int>? SpeakProgress;

        public List<SyntheticVoiceDescriptor> Voices { get; } = new();

        public bool IsSpeaking { get; set; }

        public int Rate { get; set; }

        public int Volume { get; set; }

        public string? SelectedVoice { get; private set; }

        public string? LastText { get; private set; }

        public int SpeakCalls { get; private set; }

        public int StopCalls { get; private set; }

        public void SelectVoice(string name) => SelectedVoice = name;

        public void Speak(string text)
        {
            SpeakCalls++;
            LastText = text;
            IsSpeaking = true;
        }

        public void Stop()
        {
            StopCalls++;
            IsSpeaking = false;
        }

        public IReadOnlyList<SyntheticVoiceDescriptor> GetVoices() => Voices;
    }

    private sealed class FakeSpeechEngine : ISpeechEngine
    {
        public event Action? SpeakCompleted;

        public event Action<int, int>? SpeakProgress;

        public bool IsSpeaking { get; private set; }

        public string? LastText { get; private set; }

        public int SpeakCalls { get; private set; }

        public int StopCalls { get; private set; }

        public IReadOnlyList<VoiceInfo> GetVoices() => Array.Empty<VoiceInfo>();

        public void Speak(string text, VoiceOptions options)
        {
            SpeakCalls++;
            LastText = text;
            IsSpeaking = true;
        }

        public void Stop()
        {
            StopCalls++;
            IsSpeaking = false;
        }

        public void EmitCompleted() => SpeakCompleted?.Invoke();

        public void EmitProgress(int position, int count) => SpeakProgress?.Invoke(position, count);
    }
}
