using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ReadAloud.Models;
using ReadAloud.Services;

namespace ReadAloud.Tests;

public sealed class PlaybackTests
{
    [Fact]
    public async Task PlayAsync_SetsPlayingAndSpeaksText()
    {
        var h = new Harness();
        var text = "First sentence. Second sentence. Third sentence.";

        await h.Playback.PlayAsync(text, new VoiceOptions("Voice", 0, 100));

        Assert.Contains(PlaybackStatus.Playing, h.Statuses);
        Assert.Equal(PlaybackStatus.Completed, h.Playback.State.Status);
        Assert.Equal(text, h.Speech.LastText);
        Assert.Equal(3, h.Playback.State.Sentences.Count);
        Assert.Equal(1, h.Playback.State.Progress);
    }

    [Fact]
    public async Task PauseAndResume_UpdateStatus()
    {
        var h = new Harness();
        h.Speech.AutoComplete = false;
        await h.Playback.PlayAsync("One. Two.", new VoiceOptions("Voice", 0, 100));

        h.Playback.Pause();
        Assert.Equal(PlaybackStatus.Paused, h.Playback.State.Status);
        Assert.Equal(1, h.Speech.PauseCalls);

        h.Playback.Resume();
        Assert.Equal(PlaybackStatus.Playing, h.Playback.State.Status);
        Assert.Equal(1, h.Speech.ResumeCalls);
        Assert.Equal(1, h.Speech.SpeakCalls);
    }

    [Fact]
    public async Task NewRead_SupersedesInFlightSpeech()
    {
        var h = new Harness();
        h.Speech.AutoComplete = false;
        await h.Playback.PlayAsync("First read.", new VoiceOptions("Voice", 0, 100));

        await h.Playback.PlayAsync("Second read.", new VoiceOptions("Voice", 0, 100));

        Assert.Equal("Second read.", h.Speech.LastText);
        Assert.Equal(1, h.Speech.StopCalls);
        Assert.Equal(2, h.Speech.SpeakCalls);
        Assert.Equal(PlaybackStatus.Playing, h.Playback.State.Status);
    }

    [Fact]
    public async Task CompletedRead_RecordsMeasuredDuration()
    {
        var h = new Harness();
        h.Speech.AutoComplete = false;

        await h.Playback.PlayAsync("One two three.", new VoiceOptions("Voice", 0, 100));
        await Task.Delay(15);
        h.Speech.Complete();

        Assert.True(h.Playback.State.Elapsed > TimeSpan.Zero);
        Assert.Equal(PlaybackStatus.Completed, h.Playback.State.Status);
    }

    [Fact]
    public void EstimateRemaining_UsesCurrentSpeed()
    {
        var slow = new Harness();
        slow.Speech.AutoComplete = false;
        var fast = new Harness();
        fast.Speech.AutoComplete = false;
        fast.Config.PlaybackSpeed = 2.0;

        _ = slow.Playback.PlayAsync("One two three four five six seven eight.", new VoiceOptions("Voice", 0, 100));
        _ = fast.Playback.PlayAsync("One two three four five six seven eight.", new VoiceOptions("Voice", 0, 100));

        Assert.True(fast.Playback.State.EstimatedRemaining < slow.Playback.State.EstimatedRemaining);
    }

    [Fact]
    public async Task Progress_AdvancesCurrentSentence()
    {
        var h = new Harness();
        h.Speech.AutoComplete = false;
        await h.Playback.PlayAsync("First sentence. Second sentence. Third sentence.", new VoiceOptions("Voice", 0, 100));

        h.Speech.RaiseProgress("First sentence. ".Length + 1, 1);

        Assert.Equal(1, h.Playback.State.CurrentSentenceIndex);
    }

    [Fact]
    public async Task Stop_SetsStoppedAndClearsProgress()
    {
        var h = new Harness();
        h.Speech.AutoComplete = false;
        await h.Playback.PlayAsync("One. Two.", new VoiceOptions("Voice", 0, 100));

        h.Playback.Stop();

        Assert.Equal(PlaybackStatus.Stopped, h.Playback.State.Status);
        Assert.Equal(TimeSpan.Zero, h.Playback.State.Elapsed);
        Assert.Equal(TimeSpan.Zero, h.Playback.State.EstimatedRemaining);
        Assert.Equal(0, h.Playback.State.Progress);
        Assert.Equal(1, h.Speech.StopCalls);
    }

    [Fact]
    public async Task SkipNext_MovesOneSentenceAndContinuesToEnd()
    {
        var h = new Harness();
        h.Speech.AutoComplete = false;
        await h.Playback.PlayAsync("First sentence. Second sentence. Third sentence.", new VoiceOptions("Voice", 0, 100));

        h.Playback.SkipNext();

        Assert.Equal("Second sentence. Third sentence.", h.Playback.State.Text);
        Assert.Equal(1, h.Playback.State.CurrentSentenceIndex);
        Assert.Equal(PlaybackStatus.Playing, h.Playback.State.Status);

        h.Playback.SkipNext();
        Assert.Equal("Third sentence.", h.Playback.State.Text);
        Assert.Equal(2, h.Playback.State.CurrentSentenceIndex);
    }

    [Fact]
    public async Task SkipPrevious_AtFirstSentence_StaysAtFirst()
    {
        var h = new Harness();
        h.Speech.AutoComplete = false;
        await h.Playback.PlayAsync("First sentence. Second sentence. Third sentence.", new VoiceOptions("Voice", 0, 100));

        h.Playback.SkipPrevious();

        Assert.Equal("First sentence. Second sentence. Third sentence.", h.Playback.State.Text);
        Assert.Equal(0, h.Playback.State.CurrentSentenceIndex);
        Assert.Equal(PlaybackStatus.Playing, h.Playback.State.Status);
    }

    [Theory]
    [InlineData(0.5, -10)]
    [InlineData(1.0, 0)]
    [InlineData(2.0, 10)]
    [InlineData(3.0, 10)]
    public void SetSpeed_MapsSpeedToSapiRate(double speed, int expectedRate)
    {
        var h = new Harness();

        h.Playback.SetSpeed(speed);

        Assert.Equal(expectedRate, h.Config.SapiRate);
        Assert.Equal(speed, h.Config.PlaybackSpeed);
        Assert.Equal(speed, h.Playback.State.Speed);
    }

    [Fact]
    public async Task ProgressAndRemaining_ArePublished()
    {
        var h = new Harness();
        h.Speech.AutoComplete = false;
        await h.Playback.PlayAsync("One two three four five six seven eight.", new VoiceOptions("Voice", 0, 100));
        var initialRemaining = h.Playback.State.EstimatedRemaining;

        h.Speech.RaiseProgress(h.Speech.LastText!.Length / 2, 1);

        Assert.True(h.Playback.State.Progress > 0);
        Assert.True(h.Playback.State.Elapsed > TimeSpan.Zero);
        Assert.True(h.Playback.State.EstimatedRemaining < initialRemaining);
        Assert.Contains(h.States, state => state.Status == PlaybackStatus.Playing && state.Progress > 0);
    }

    private sealed class Harness
    {
        public Harness()
        {
            Speech = new FakeSpeechEngine();
            Playback = new PlaybackService(Speech, () => Config);
            Playback.StateChanged += state =>
            {
                Statuses.Add(state.Status);
                States.Add(state);
            };
        }

        public AppConfig Config { get; } = new();

        public FakeSpeechEngine Speech { get; }

        public PlaybackService Playback { get; }

        public List<PlaybackStatus> Statuses { get; } = new();

        public List<PlaybackState> States { get; } = new();
    }

    private sealed class FakeSpeechEngine : ISpeechEngine
    {
        public event Action? SpeakCompleted;

        public event Action<int, int>? SpeakProgress;

        public bool AutoComplete { get; set; } = true;

        public bool IsSpeaking { get; private set; }

        public string? LastText { get; private set; }

        public int SpeakCalls { get; private set; }

        public int StopCalls { get; private set; }

        public int PauseCalls { get; private set; }

        public int ResumeCalls { get; private set; }

        public IReadOnlyList<VoiceInfo> GetVoices() => Array.Empty<VoiceInfo>();

        public void Speak(string text, VoiceOptions options)
        {
            SpeakCalls++;
            LastText = text;
            IsSpeaking = true;

            if (AutoComplete)
            {
                IsSpeaking = false;
                SpeakCompleted?.Invoke();
            }
        }

        public void Stop()
        {
            StopCalls++;
            IsSpeaking = false;
        }

        public void Pause() => PauseCalls++;

        public void Resume() => ResumeCalls++;

        public void Complete()
        {
            IsSpeaking = false;
            SpeakCompleted?.Invoke();
        }

        public void RaiseProgress(int position, int count)
            => SpeakProgress?.Invoke(position, count);
    }
}
