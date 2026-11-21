using System;
using System.Collections.Generic;
using System.Windows.Input;
using ReadAloud.Services;

namespace ReadAloud.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IThemeService _themes;
    private string _theme = "aurora";
    private string _selectedNavigation = "Home";
    private HomeViewModel? _home;

    public MainViewModel(IThemeService themes, IHotkeyRegistrar registrar)
    {
        _themes = themes;
        NavigateCommand = new RelayCommand(parameter =>
        {
            var page = parameter as string;
            if (!string.IsNullOrEmpty(page) && NavigationEnabled.TryGetValue(page, out var enabled) && enabled)
            {
                SelectedNavigation = page;
            }
        });
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

    public VoiceLibraryViewModel? VoiceLibrary { get; set; }

    public string SelectedNavigation
    {
        get => _selectedNavigation;
        private set => SetProperty(ref _selectedNavigation, value);
    }

    public ICommand NavigateCommand { get; }

    public IReadOnlyList<string> NavigationItems { get; } =
        new[] { "Home", "Voice Library", "History", "Settings" };

    public IReadOnlyDictionary<string, bool> NavigationEnabled { get; } =
        new Dictionary<string, bool>
        {
            ["Home"] = true,
            ["Voice Library"] = true,
            ["History"] = false,
            ["Settings"] = true
        };

    public void ApplyTheme(string theme)
    {
        _themes.Apply(theme);
        Theme = _themes.CurrentTheme;
    }
}
