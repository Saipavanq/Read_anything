using System;
using System.Windows.Input;
using ReadAloud.Models;
using ReadAloud.Services;

namespace ReadAloud.ViewModels;

public sealed class MiniPlayerViewModel : ObservableObject
{
    private readonly PlaybackService _playback;
    private readonly MiniPlayerService _miniPlayer;
    private PlaybackState _state;

    public MiniPlayerViewModel(PlaybackService playback, MiniPlayerService miniPlayer)
    {
        _playback = playback;
        _miniPlayer = miniPlayer;
        _state = playback.State;
        playback.StateChanged += UpdateState;

        PauseResumeCommand = new RelayCommand(_ => TogglePauseResume());
        StopCommand = new RelayCommand(_ => _playback.Stop());
        SkipNextCommand = new RelayCommand(_ => _playback.SkipNext());
        SkipPreviousCommand = new RelayCommand(_ => _playback.SkipPrevious());
        ExpandCommand = new RelayCommand(_ => _miniPlayer.RequestExpand());
        CloseCommand = new RelayCommand(_ => _miniPlayer.Hide());
    }

    public PlaybackState State
    {
        get => _state;
        private set => SetProperty(ref _state, value);
    }

    public string StatusText => _state.Status switch
    {
        PlaybackStatus.Playing => "Playing",
        PlaybackStatus.Paused => "Paused",
        PlaybackStatus.Completed => "Finished",
        _ => "Ready"
    };

    public string PauseResumeLabel => _state.Status == PlaybackStatus.Paused ? "Resume" : "Pause";

    public bool CanPauseResume => _state.Status is PlaybackStatus.Playing or PlaybackStatus.Paused;

    public int ProgressPercent => (int)Math.Round(_state.Progress * 100);

    public string ElapsedText => FormatTime(_state.Elapsed);

    public string RemainingText => FormatTime(_state.EstimatedRemaining);

    public ICommand PauseResumeCommand { get; }

    public ICommand StopCommand { get; }

    public ICommand SkipNextCommand { get; }

    public ICommand SkipPreviousCommand { get; }

    public ICommand ExpandCommand { get; }

    public ICommand CloseCommand { get; }

    private void TogglePauseResume()
    {
        if (_state.Status == PlaybackStatus.Playing)
        {
            _playback.Pause();
        }
        else if (_state.Status == PlaybackStatus.Paused)
        {
            _playback.Resume();
        }
    }

    public void UpdateState(PlaybackState state)
    {
        State = state;
        RaisePropertyChanged(nameof(StatusText));
        RaisePropertyChanged(nameof(PauseResumeLabel));
        RaisePropertyChanged(nameof(CanPauseResume));
        RaisePropertyChanged(nameof(ProgressPercent));
        RaisePropertyChanged(nameof(ElapsedText));
        RaisePropertyChanged(nameof(RemainingText));
    }

    private static string FormatTime(TimeSpan value)
        => value.TotalMinutes >= 1
            ? value.ToString(@"m\:ss")
            : value.ToString(@"s\.f");
}
