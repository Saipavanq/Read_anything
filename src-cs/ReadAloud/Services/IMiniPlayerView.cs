using ReadAloud.Models;

namespace ReadAloud.Services;

public interface IMiniPlayerView
{
    event Action? ExpandRequested;

    event Action? CloseRequested;

    bool Topmost { get; set; }

    void Show();

    void Hide();

    void Update(PlaybackState state);
}
