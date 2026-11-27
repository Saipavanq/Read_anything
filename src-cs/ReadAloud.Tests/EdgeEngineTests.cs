using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReadAloud.Models;
using ReadAloud.Services;

namespace ReadAloud.Tests;

public sealed class EdgeEngineTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "readaloud-edge-tests-" + Guid.NewGuid().ToString("N"));

    public EdgeEngineTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Temporary files are best-effort.
        }
        catch (UnauthorizedAccessException)
        {
            // Temporary files are best-effort.
        }
    }

    [Fact]
    public void EdgeCatalog_ParsesMicrosoftJson()
    {
        var json = """
        [
          {"Name":"Microsoft Aria Online (Natural) - English (United States)","ShortName":"en-US-AriaNeural","Locale":"en-US","Gender":"Female"},
          {"Name":"Microsoft Guy Online (Natural) - English (United States)","ShortName":"en-US-GuyNeural","Locale":"en-US","Gender":"Male"},
          {"Name":"Microsoft Katja Online (Natural) - German","ShortName":"de-DE-KatjaNeural","Locale":"de-DE","Gender":"Female"}
        ]
        """;

        var voices = EdgeVoiceCatalog.Parse(json);

        Assert.Equal(2, voices.Count);
        Assert.Equal("en-US-AriaNeural", voices[0].Name);
        Assert.Equal("en-US-AriaNeural", voices[0].Description);
        Assert.Equal("en-US", voices[0].Language);
        Assert.Equal("Female", voices[0].Gender);
        Assert.False(voices[0].IsDefault);
        Assert.Equal("en-US-GuyNeural", voices[1].Name);
        Assert.Equal("Male", voices[1].Gender);
        Assert.DoesNotContain(voices, voice => voice.Name == "de-DE-KatjaNeural");
    }

    [Fact]
    public void EdgeCatalog_ReturnsOnlyTheFiveCuratedVoices()
    {
        var json = """
        [
          {"ShortName":"de-DE-KatjaNeural","Locale":"de-DE","Gender":"Female"},
          {"ShortName":"en-US-ChristopherNeural","Locale":"en-US","Gender":"Male"},
          {"ShortName":"en-US-GuyNeural","Locale":"en-US","Gender":"Male"},
          {"ShortName":"en-GB-SoniaNeural","Locale":"en-GB","Gender":"Female"},
          {"ShortName":"en-US-JennyNeural","Locale":"en-US","Gender":"Female"},
          {"ShortName":"en-US-AriaNeural","Locale":"en-US","Gender":"Female"}
        ]
        """;

        var voices = EdgeVoiceCatalog.Parse(json);

        Assert.Equal(
            new[]
            {
                "en-US-AriaNeural", "en-US-JennyNeural", "en-GB-SoniaNeural",
                "en-US-GuyNeural", "en-US-ChristopherNeural"
            },
            voices.Select(voice => voice.Name));
        Assert.Equal(3, voices.Count(voice => voice.Gender == "Female"));
        Assert.Equal(2, voices.Count(voice => voice.Gender == "Male"));
    }

    [Fact]
    public void EdgeCatalog_NoCacheReturnsFallback()
    {
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var catalog = new EdgeVoiceCatalog(
            Path.Combine(_root, "missing", "edge_voices.json"),
            _ => release.Task);

        try
        {
            var timer = Stopwatch.StartNew();
            var voices = catalog.GetVoices();
            timer.Stop();

            Assert.Equal(
                new[]
                {
                    "en-US-AriaNeural", "en-US-JennyNeural", "en-GB-SoniaNeural",
                    "en-US-GuyNeural", "en-US-ChristopherNeural"
                },
                voices.Select(voice => voice.Name));
            Assert.True(timer.Elapsed < TimeSpan.FromSeconds(2), "GetVoices must not wait for the HTTP refresh.");
        }
        finally
        {
            release.SetResult(null);
        }
    }

    [Fact]
    public async Task EdgeEngine_UsesVoicesInPriorityOrder()
    {
        var synthesizer = new FakeSynthesizer(_root);
        var player = new FakeAudioPlayer();
        var fallback = new FakeFallbackEngine();
        var config = new AppConfig { EdgeVoicePriority = new List<string> { "v1", "v2" } };
        var engine = new EdgeSpeechEngine(
            () => config, synthesizer, player, fallback, OfflineCatalog());
        var completions = 0;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SpeakCompleted += () =>
        {
            Interlocked.Increment(ref completions);
            completed.TrySetResult();
        };

        engine.Speak("Read this.", new VoiceOptions("Voice", 0, 100));

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "v1" }, synthesizer.Calls);
        Assert.Equal("Read this.", synthesizer.LastText);
        Assert.Equal(1, player.PlayCalls);
        Assert.Equal(100, player.LastVolume);
        Assert.Equal(0, fallback.SpeakCalls);
        Assert.Equal(1, completions);
    }

    [Fact]
    public async Task EdgeEngine_FallsBackToPiperWhenAllVoicesFail()
    {
        var synthesizer = new FakeSynthesizer(_root) { Fail = true };
        var player = new FakeAudioPlayer();
        var fallback = new FakeFallbackEngine { CompleteOnSpeak = true };
        var config = new AppConfig { EdgeVoicePriority = new List<string> { "v1", "v2" } };
        var engine = new EdgeSpeechEngine(
            () => config, synthesizer, player, fallback, OfflineCatalog());
        var completions = 0;
        engine.SpeakCompleted += () => Interlocked.Increment(ref completions);

        engine.Speak("Offline text.", new VoiceOptions("Voice", 0, 100));

        await fallback.Called.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, fallback.SpeakCalls);
        Assert.Equal("Offline text.", fallback.LastText);
        Assert.Equal(new[] { "v1", "v2" }, synthesizer.Calls);
        Assert.Equal(0, player.PlayCalls);
        Assert.Equal(1, completions);
    }

    [Fact]
    public async Task EdgeEngine_TreatsEmptySynthesisAsFailureAndFallsBack()
    {
        var synthesizer = new FakeSynthesizer(_root) { CreateEmptyFile = true };
        var player = new FakeAudioPlayer();
        var fallback = new FakeFallbackEngine { CompleteOnSpeak = true };
        var config = new AppConfig { EdgeVoicePriority = new List<string> { "v1" } };
        var engine = new EdgeSpeechEngine(
            () => config, synthesizer, player, fallback, OfflineCatalog());
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SpeakCompleted += () => completed.TrySetResult();

        engine.Speak("Empty file.", new VoiceOptions("Voice", 0, 100));

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, fallback.SpeakCalls);
        Assert.Equal(0, player.PlayCalls);
    }

    [Fact]
    public async Task EdgeEngine_PreviewVoiceTriedFirst()
    {
        var synthesizer = new FakeSynthesizer(_root);
        var player = new FakeAudioPlayer();
        var fallback = new FakeFallbackEngine();
        var config = new AppConfig { EdgeVoicePriority = new List<string> { "v1", "v2" } };
        var originalPriority = config.EdgeVoicePriority.ToArray();
        var engine = new EdgeSpeechEngine(
            () => config, synthesizer, player, fallback, OfflineCatalog());
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SpeakCompleted += () => completed.TrySetResult();

        engine.Speak("Preview text.", new VoiceOptions("Voice", 0, 100, VoiceId: "preview"));

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "preview" }, synthesizer.Calls);
        Assert.Equal(originalPriority, config.EdgeVoicePriority);
    }

    [Fact]
    public void EdgeEngine_GetVoicesIncludesPiperVoice()
    {
        var fallback = new FakeFallbackEngine();
        fallback.Voices.Add(new VoiceInfo("en_US-amy-medium", "amy.onnx", "Not specified", "Not specified", false));
        var engine = new EdgeSpeechEngine(
            () => new AppConfig(),
            new FakeSynthesizer(_root),
            new FakeAudioPlayer(),
            fallback,
            OfflineCatalog());

        var voices = engine.GetVoices();

        Assert.Equal(6, voices.Count);
        Assert.Contains(voices, voice => voice.Name == "en-US-AriaNeural");
        Assert.Contains(voices, voice => voice.Name == "en-US-ChristopherNeural");
        Assert.Contains(voices, voice => voice.Name == "en_US-amy-medium");
    }

    [Fact]
    public async Task EdgeEngine_PreviewPiperVoiceUsesFallback()
    {
        var synthesizer = new FakeSynthesizer(_root);
        var player = new FakeAudioPlayer();
        var fallback = new FakeFallbackEngine();
        fallback.Voices.Add(new VoiceInfo("en_US-amy-medium", "amy.onnx", "Not specified", "Not specified", false));
        var engine = new EdgeSpeechEngine(
            () => new AppConfig(), synthesizer, player, fallback, OfflineCatalog());
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SpeakCompleted += () => completed.TrySetResult();

        engine.Speak(
            "Local preview.",
            new VoiceOptions("amy.onnx", 0, 100, VoiceId: "en_US-amy-medium"));

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(synthesizer.Calls);
        Assert.Equal(0, player.PlayCalls);
        Assert.Equal(1, fallback.SpeakCalls);
        Assert.Equal("Local preview.", fallback.LastText);
    }

    [Fact]
    public async Task EdgeEngine_FallbackStaysSpeakingUntilCompletion()
    {
        var synthesizer = new FakeSynthesizer(_root) { Fail = true };
        var fallback = new FakeFallbackEngine { DeferCompletion = true };
        var config = new AppConfig { EdgeVoicePriority = new List<string> { "v1" } };
        var engine = new EdgeSpeechEngine(
            () => config, synthesizer, new FakeAudioPlayer(), fallback, OfflineCatalog());
        var completions = 0;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SpeakCompleted += () =>
        {
            Interlocked.Increment(ref completions);
            completed.TrySetResult();
        };

        engine.Speak("Async fallback.", new VoiceOptions("Voice", 0, 100));

        await fallback.Called.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(100);
        Assert.True(engine.IsSpeaking, "Fallback speech is still running after Speak returns.");
        Assert.Equal(0, completions);

        fallback.EmitCompletion();

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(engine.IsSpeaking);
        Assert.Equal(1, completions);
    }

    [Fact]
    public async Task EdgeEngine_ThrowingFallbackRaisesOneCompletion()
    {
        var synthesizer = new FakeSynthesizer(_root) { Fail = true };
        var fallback = new FakeFallbackEngine { ThrowOnSpeak = true };
        var config = new AppConfig { EdgeVoicePriority = new List<string> { "v1" } };
        var engine = new EdgeSpeechEngine(
            () => config, synthesizer, new FakeAudioPlayer(), fallback, OfflineCatalog());
        var completions = 0;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SpeakCompleted += () =>
        {
            Interlocked.Increment(ref completions);
            completed.TrySetResult();
        };

        engine.Speak("Throwing fallback.", new VoiceOptions("Voice", 0, 100));

        await fallback.Called.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, completions);
        Assert.False(engine.IsSpeaking);
    }

    [Fact]
    public void NAudioPlayer_StopWithoutPlayback_IsSafeAndRepeatable()
    {
        var player = new NAudioPlayer();

        player.Stop();
        player.Stop();

        var timer = Stopwatch.StartNew();
        player.Play(Path.Combine(_root, "missing.mp3"), 100);
        timer.Stop();
        player.Stop();

        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task EdgeEngine_FirstVoiceFailsSecondSucceeds()
    {
        var synthesizer = new FakeSynthesizer(_root);
        synthesizer.FailVoices.Add("v1");
        var player = new FakeAudioPlayer();
        var fallback = new FakeFallbackEngine();
        var config = new AppConfig { EdgeVoicePriority = new List<string> { "v1", "v2" } };
        var engine = new EdgeSpeechEngine(
            () => config, synthesizer, player, fallback, OfflineCatalog());
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SpeakCompleted += () => completed.TrySetResult();

        engine.Speak("Second voice.", new VoiceOptions("Voice", 0, 100));

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "v1", "v2" }, synthesizer.Calls);
        Assert.Equal(Path.Combine(_root, "synthesis-2.mp3"), player.LastFile);
        Assert.Equal(0, fallback.SpeakCalls);
    }

    [Fact]
    public async Task EdgeEngine_DeletesTempFilesAfterPlayback()
    {
        var synthesizer = new FakeSynthesizer(_root);
        synthesizer.FailVoices.Add("v1");
        var player = new FakeAudioPlayer();
        var config = new AppConfig { EdgeVoicePriority = new List<string> { "v1", "v2" } };
        var engine = new EdgeSpeechEngine(
            () => config, synthesizer, player, new FakeFallbackEngine(), OfflineCatalog());
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SpeakCompleted += () => completed.TrySetResult();

        engine.Speak("Cleanup text.", new VoiceOptions("Voice", 0, 100));

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(player.LastFileExistedAtPlay, "The playing file must survive until playback starts.");
        foreach (var path in synthesizer.CreatedPaths)
        {
            Assert.False(File.Exists(path));
        }

        Assert.Empty(Directory.GetFiles(_root, "*.mp3"));
    }

    private EdgeVoiceCatalog OfflineCatalog()
        => new(
            Path.Combine(_root, "missing", "edge_voices.json"),
            _ => Task.FromResult<string?>(null));

    private sealed class FakeSynthesizer : IEdgeSynthesizer
    {
        private readonly string _root;

        public FakeSynthesizer(string root) => _root = root;

        public bool Fail { get; init; }

        public bool CreateEmptyFile { get; init; }

        public HashSet<string> FailVoices { get; } = new(StringComparer.Ordinal);

        public List<string> Calls { get; } = new();

        public List<string> CreatedPaths { get; } = new();

        public string? LastText { get; private set; }

        public Task<string> SynthesizeAsync(string text, string voice, string rate, CancellationToken token)
        {
            Calls.Add(voice);
            LastText = text;
            if (Fail || FailVoices.Contains(voice))
            {
                throw new InvalidOperationException("offline");
            }

            var path = Path.Combine(_root, "synthesis-" + Calls.Count + ".mp3");
            File.WriteAllText(path, CreateEmptyFile ? string.Empty : "fake mp3");
            CreatedPaths.Add(path);
            return Task.FromResult(path);
        }
    }

    private sealed class FakeAudioPlayer : IAudioPlayer
    {
        public int PlayCalls { get; private set; }

        public string? LastFile { get; private set; }

        public int LastVolume { get; private set; }

        public bool LastFileExistedAtPlay { get; private set; }

        public int StopCalls { get; private set; }

        public void Play(string file, int volume)
        {
            PlayCalls++;
            LastFile = file;
            LastVolume = volume;
            LastFileExistedAtPlay = File.Exists(file);
        }

        public void Stop() => StopCalls++;
    }

    private sealed class FakeFallbackEngine : ISpeechEngine
    {
        public event Action? SpeakCompleted;

        public event Action<int, int>? SpeakProgress;

        public bool CompleteOnSpeak { get; init; } = true;

        public bool DeferCompletion { get; init; }

        public bool ThrowOnSpeak { get; init; }

        public TaskCompletionSource Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<VoiceInfo> Voices { get; } = new();

        public int SpeakCalls { get; private set; }

        public string? LastText { get; private set; }

        public bool IsSpeaking { get; private set; }

        public IReadOnlyList<VoiceInfo> GetVoices() => Voices.ToList();

        public void Speak(string text, VoiceOptions options)
        {
            SpeakCalls++;
            LastText = text;
            IsSpeaking = true;
            try
            {
                if (ThrowOnSpeak)
                {
                    throw new InvalidOperationException("fallback failed");
                }

                if (CompleteOnSpeak && !DeferCompletion)
                {
                    EmitCompletion();
                }
            }
            finally
            {
                Called.TrySetResult();
            }
        }

        public void EmitCompletion()
        {
            if (!IsSpeaking)
            {
                return;
            }

            IsSpeaking = false;
            SpeakProgress?.Invoke(0, LastText?.Length ?? 0);
            SpeakCompleted?.Invoke();
        }

        public void Stop() => IsSpeaking = false;
    }
}
