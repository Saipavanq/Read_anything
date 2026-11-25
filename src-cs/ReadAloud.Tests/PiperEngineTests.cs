using System;
using System.Collections.Generic;
using System.Linq;
using ReadAloud.Models;
using ReadAloud.Services;

namespace ReadAloud.Tests;

public sealed class PiperEngineTests
{
    [Fact]
    public void ParseVoiceList_MapsOnnxFiles()
    {
        var json = """[{"name":"en_US-amy-medium","path":"C:\\v\\en_US-amy-medium.onnx"}]""";

        var voices = PiperSpeechEngine.ParseVoiceList(json);

        var voice = Assert.Single(voices);
        Assert.Equal("en_US-amy-medium", voice.Name);
        Assert.Equal(@"C:\v\en_US-amy-medium.onnx", voice.Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("not-json")]
    public void ParseVoiceList_EmptyAndMalformed_ReturnEmpty(string json)
    {
        Assert.Empty(PiperSpeechEngine.ParseVoiceList(json));
    }

    [Fact]
    public void SelectableEngine_RoutesByConfig()
    {
        var sapi = new FakeSpeechEngine();
        var piper = new FakeSpeechEngine();
        var config = new AppConfig { TtsEngine = "sapi" };
        var engine = new SelectableSpeechEngine(sapi, piper, () => config);
        var options = new VoiceOptions("voice", 0, 100);

        engine.Speak("SAPI text", options);

        Assert.Equal(1, sapi.SpeakCalls);
        Assert.Equal("SAPI text", sapi.LastText);
        Assert.Equal(0, piper.SpeakCalls);

        config.TtsEngine = "piper";
        engine.Speak("Piper text", options);

        Assert.Equal(1, piper.SpeakCalls);
        Assert.Equal("Piper text", piper.LastText);
        Assert.Equal(1, sapi.SpeakCalls);
    }

    [Fact]
    public void SelectableEngine_ForwardsEventsAndStop()
    {
        var sapi = new FakeSpeechEngine();
        var piper = new FakeSpeechEngine();
        var config = new AppConfig { TtsEngine = "piper" };
        var engine = new SelectableSpeechEngine(sapi, piper, () => config);
        var completed = 0;
        var progress = new List<(int Position, int Count)>();
        engine.SpeakCompleted += () => completed++;
        engine.SpeakProgress += (position, count) => progress.Add((position, count));

        piper.EmitProgress(3, 10);
        piper.EmitCompleted();

        Assert.Equal(1, completed);
        Assert.Equal((3, 10), Assert.Single(progress));

        engine.Stop();

        Assert.Equal(1, piper.StopCalls);
        Assert.Equal(0, sapi.StopCalls);
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
