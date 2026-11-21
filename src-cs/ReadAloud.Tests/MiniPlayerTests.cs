using System;
using System.Collections.Generic;
using ReadAloud.Models;
using ReadAloud.Services;
using ReadAloud.ViewModels;

namespace ReadAloud.Tests;

public sealed class MiniPlayerTests
{
    [Fact]
    public void ShowForSelectionRead_ShowsWhenEnabled()
    {
        var h = new Harness();
        h.Config.MiniPlayerEnabled = true;
        h.Config.MiniPlayerAlwaysOnTop = true;

        h.Service.ShowForSelectionRead();

        Assert.True(h.Service.IsVisible);
        Assert.Equal(1, h.View.ShowCalls);
        Assert.True(h.View.Topmost);
    }

    [Fact]
    public void ShowForSelectionRead_DoesNothingWhenDisabled()
    {
        var h = new Harness();
        h.Config.MiniPlayerEnabled = false;

        h.Service.ShowForSelectionRead();

        Assert.False(h.Service.IsVisible);
        Assert.Equal(0, h.View.ShowCalls);
    }

    [Fact]
    public void SetAlwaysOnTop_SetsViewTopmost()
    {
        var h = new Harness();

        h.Service.SetAlwaysOnTop(true);

        Assert.True(h.View.Topmost);
        Assert.True(h.Config.MiniPlayerAlwaysOnTop);
    }

    [Fact]
    public void UpdateState_ViewsPlaybackState()
    {
        var h = new Harness();
        var state = State(PlaybackStatus.Playing);

        h.Service.UpdateState(state);

        Assert.Same(state, h.View.LastState);
        Assert.Equal(0, h.View.HideCalls);
    }

    [Fact]
    public void UpdateState_AutoHidesOnCompleted()
    {
        var h = new Harness();
        h.Config.MiniPlayerAutoHide = true;
        h.Service.ShowForSelectionRead();

        h.Service.UpdateState(State(PlaybackStatus.Completed));

        Assert.Equal(1, h.View.HideCalls);
        Assert.False(h.Service.IsVisible);
    }

    [Fact]
    public void Close_HidesOnlyMiniPlayer()
    {
        var h = new Harness();
        var expanded = 0;
        h.Service.ExpandRequested += () => expanded++;
        h.Service.ShowForSelectionRead();

        h.View.RaiseCloseRequested();

        Assert.Equal(1, h.View.HideCalls);
        Assert.False(h.Service.IsVisible);
        Assert.Equal(0, expanded);
    }

    [Fact]
    public void ExpandRequested_IsForwarded()
    {
        var h = new Harness();
        var expanded = 0;
        h.Service.ExpandRequested += () => expanded++;

        h.View.RaiseExpandRequested();

        Assert.Equal(1, expanded);
    }

    [Fact]
    public async Task ViewModel_FollowsPlaybackStateChanges()
    {
        var h = new Harness();
        h.Speech.AutoComplete = false;

        await h.Playback.PlayAsync("First. Second.", new VoiceOptions("Voice", 0, 100));

        Assert.Equal(PlaybackStatus.Playing, h.ViewModel.State.Status);
        Assert.Equal("First. Second.", h.ViewModel.State.Text);
    }

    [Fact]
    public async Task ViewModel_PauseResumeCommand_TogglesStatus()
    {
        var h = new Harness();
        h.Speech.AutoComplete = false;
        await h.Playback.PlayAsync("First. Second.", new VoiceOptions("Voice", 0, 100));

        h.ViewModel.PauseResumeCommand.Execute(null);
        Assert.Equal(PlaybackStatus.Paused, h.ViewModel.State.Status);

        h.ViewModel.PauseResumeCommand.Execute(null);
        Assert.Equal(PlaybackStatus.Playing, h.ViewModel.State.Status);
    }

    [Fact]
    public async Task ViewModel_StopCommand_StopsPlayback()
    {
        var h = new Harness();
        h.Speech.AutoComplete = false;
        await h.Playback.PlayAsync("First. Second.", new VoiceOptions("Voice", 0, 100));

        h.ViewModel.StopCommand.Execute(null);

        Assert.Equal(PlaybackStatus.Stopped, h.ViewModel.State.Status);
        Assert.Equal(1, h.Speech.StopCalls);
    }

    [Fact]
    public async Task ViewModel_SkipCommands_ChangeSentence()
    {
        var h = new Harness();
        h.Speech.AutoComplete = false;
        await h.Playback.PlayAsync("First. Second. Third.", new VoiceOptions("Voice", 0, 100));

        h.ViewModel.SkipNextCommand.Execute(null);
        Assert.Equal(1, h.ViewModel.State.CurrentSentenceIndex);

        h.ViewModel.SkipPreviousCommand.Execute(null);
        Assert.Equal(0, h.ViewModel.State.CurrentSentenceIndex);
    }

    [Fact]
    public void ViewModel_ExpandAndCloseCommands_UseMiniPlayerService()
    {
        var h = new Harness();
        var expanded = 0;
        h.Service.ExpandRequested += () => expanded++;
        h.Service.ShowForSelectionRead();

        h.ViewModel.ExpandCommand.Execute(null);
        h.ViewModel.CloseCommand.Execute(null);

        Assert.Equal(1, expanded);
        Assert.Equal(1, h.View.HideCalls);
        Assert.False(h.Service.IsVisible);
    }

    private static PlaybackState State(PlaybackStatus status)
        => new(
            "Sample text.",
            new[] { "Sample text." },
            0,
            status,
            1.0,
            "Voice",
            TimeSpan.Zero,
            TimeSpan.Zero,
            0);

    private sealed class Harness
    {
        public Harness()
        {
            Service = new MiniPlayerService(View, () => Config);
            Playback = new PlaybackService(Speech, () => Config);
            ViewModel = new MiniPlayerViewModel(Playback, Service);
        }

        public AppConfig Config { get; } = new();

        public FakeMiniPlayerView View { get; } = new();

        public MiniPlayerService Service { get; }

        public FakeSpeechEngine Speech { get; } = new();

        public PlaybackService Playback { get; }

        public MiniPlayerViewModel ViewModel { get; }
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

        public bool AutoComplete { get; set; } = true;

        public bool IsSpeaking { get; private set; }

        public int StopCalls { get; private set; }

        public IReadOnlyList<VoiceInfo> GetVoices() => Array.Empty<VoiceInfo>();

        public void Speak(string text, VoiceOptions options)
        {
            IsSpeaking = true;

            if (AutoComplete)
            {
                IsSpeaking = false;
                _speakCompleted?.Invoke();
            }
        }

        public void Stop()
        {
            StopCalls++;
            IsSpeaking = false;
        }
    }

    private sealed class FakeMiniPlayerView : IMiniPlayerView
    {
        public int ShowCalls { get; private set; }

        public int HideCalls { get; private set; }

        public bool Topmost { get; set; }

        public PlaybackState? LastState { get; private set; }

        public event Action? ExpandRequested;

        public event Action? CloseRequested;

        public void Show() => ShowCalls++;

        public void Hide() => HideCalls++;

        public void Update(PlaybackState state) => LastState = state;

        public void RaiseExpandRequested() => ExpandRequested?.Invoke();

        public void RaiseCloseRequested() => CloseRequested?.Invoke();
    }
}
