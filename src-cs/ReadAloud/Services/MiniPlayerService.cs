using System;
using ReadAloud.Models;

namespace ReadAloud.Services;

public sealed class MiniPlayerService
{
    private readonly IMiniPlayerView _view;
    private readonly Func<AppConfig> _configProvider;
    private readonly Action<AppConfig>? _saveConfig;

    public MiniPlayerService(
        IMiniPlayerView view,
        Func<AppConfig> configProvider,
        Action<AppConfig>? saveConfig = null)
    {
        _view = view;
        _configProvider = configProvider;
        _saveConfig = saveConfig;
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
        var config = _configProvider();
        config.MiniPlayerAlwaysOnTop = value;
        _saveConfig?.Invoke(config);
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
