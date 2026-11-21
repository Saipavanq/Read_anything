using System;
using ReadAloud.Models;

namespace ReadAloud.Services;

public sealed class MiniPlayerService
{
    private readonly IMiniPlayerView _view;
    private readonly Func<AppConfig> _configProvider;

    public MiniPlayerService(IMiniPlayerView view, Func<AppConfig> configProvider)
    {
        _view = view;
        _configProvider = configProvider;
        _view.ExpandRequested += OnExpandRequested;
        _view.CloseRequested += OnCloseRequested;
    }

    public event Action? ExpandRequested;

    public bool IsVisible { get; private set; }

    public void ShowForSelectionRead()
    {
        var config = _configProvider();
        if (!config.MiniPlayerEnabled)
        {
            return;
        }

        _view.Topmost = config.MiniPlayerAlwaysOnTop;
        _view.Show();
        IsVisible = true;
    }

    public void Hide()
    {
        _view.Hide();
        IsVisible = false;
    }

    public void SetAlwaysOnTop(bool value)
    {
        _view.Topmost = value;
        _configProvider().MiniPlayerAlwaysOnTop = value;
    }

    public void UpdateState(PlaybackState state)
    {
        _view.Update(state);

        var config = _configProvider();
        if (config.MiniPlayerAutoHide && state.Status == PlaybackStatus.Completed && IsVisible)
        {
            Hide();
        }
    }

    public void RequestExpand() => ExpandRequested?.Invoke();

    private void OnExpandRequested() => ExpandRequested?.Invoke();

    private void OnCloseRequested() => Hide();
}
