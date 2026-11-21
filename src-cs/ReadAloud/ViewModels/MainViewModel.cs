using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ReadAloud.Services;

namespace ReadAloud.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IThemeService _themes;
    private string _theme = "aurora";

    public MainViewModel(IThemeService themes, IHotkeyRegistrar registrar)
    {
        _themes = themes;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Theme
    {
        get => _theme;
        private set
        {
            _theme = value;
            OnPropertyChanged();
        }
    }

    public string Greeting { get; } = "Ready to listen?";

    public string Status { get; } = "Ready";

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

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
