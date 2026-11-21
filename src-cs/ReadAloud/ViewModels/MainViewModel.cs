using System.Collections.Generic;
using ReadAloud.Services;

namespace ReadAloud.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IThemeService _themes;
    private string _theme = "aurora";
    private HomeViewModel? _home;

    public MainViewModel(IThemeService themes, IHotkeyRegistrar registrar)
    {
        _themes = themes;
    }

    public string Theme
    {
        get => _theme;
        private set => SetProperty(ref _theme, value);
    }

    public string Greeting { get; } = "Ready to listen?";

    public string Status { get; } = "Ready";

    public HomeViewModel? Home
    {
        get => _home;
        set => SetProperty(ref _home, value);
    }

    public MiniPlayerViewModel? MiniPlayer { get; set; }

    public IReadOnlyList<string> NavigationItems { get; } =
        new[] { "Home", "Voice Library", "History", "Settings" };

    public IReadOnlyDictionary<string, bool> NavigationEnabled { get; } =
        new Dictionary<string, bool>
        {
            ["Home"] = true,
            ["Voice Library"] = false,
            ["History"] = false,
            ["Settings"] = true
        };

    public void ApplyTheme(string theme)
    {
        _themes.Apply(theme);
        Theme = _themes.CurrentTheme;
    }
}
