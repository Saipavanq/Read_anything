using System;
using System.Windows;

namespace ReadAloud.Services;

public sealed class ThemeService : IThemeService
{
    private const string LightSource = "Themes/Light.xaml";
    private const string DarkSource = "Themes/Dark.xaml";
    private const string AuroraSource = "Themes/Aurora.xaml";
    private const string OrbitSource = "Themes/Orbit.xaml";
    private const string ControlsSource = "Themes/Controls.xaml";

    private readonly ResourceDictionary _applicationResources;
    private readonly ISystemThemeProvider _systemThemeProvider;
    private readonly Func<string, ResourceDictionary> _dictionaryFactory;

    public ThemeService(
        ResourceDictionary applicationResources,
        ISystemThemeProvider systemThemeProvider,
        Func<string, ResourceDictionary>? dictionaryFactory = null)
    {
        _applicationResources = applicationResources;
        _systemThemeProvider = systemThemeProvider;
        _dictionaryFactory = dictionaryFactory ?? DefaultDictionaryFactory;
        _systemThemeProvider.Changed += OnSystemThemeChanged;
    }

    public string CurrentMode { get; private set; } = "system";

    public string CurrentTheme { get; private set; } = "aurora";

    public void Apply(string mode, string theme)
    {
        var normalizedMode = NormalizeMode(mode);
        var normalizedTheme = NormalizeTheme(theme);
        var isDark = normalizedMode == "system"
            ? _systemThemeProvider.IsDark
            : normalizedMode == "dark";

        var dictionaries = new[]
        {
            _dictionaryFactory(isDark ? DarkSource : LightSource),
            _dictionaryFactory(normalizedTheme == "orbit" ? OrbitSource : AuroraSource),
            _dictionaryFactory(ControlsSource)
        };

        _applicationResources.MergedDictionaries.Clear();
        foreach (var dictionary in dictionaries)
        {
            _applicationResources.MergedDictionaries.Add(dictionary);
        }

        CurrentMode = normalizedMode;
        CurrentTheme = normalizedTheme;
    }

    private void OnSystemThemeChanged()
    {
        if (CurrentMode == "system")
        {
            Apply(CurrentMode, CurrentTheme);
        }
    }

    private static string NormalizeMode(string mode)
        => string.Equals(mode, "dark", StringComparison.OrdinalIgnoreCase)
            ? "dark"
            : string.Equals(mode, "light", StringComparison.OrdinalIgnoreCase)
                ? "light"
                : "system";

    private static string NormalizeTheme(string theme)
        => string.Equals(theme, "orbit", StringComparison.OrdinalIgnoreCase)
            ? "orbit"
            : "aurora";

    private static ResourceDictionary DefaultDictionaryFactory(string source)
        => new() { Source = new Uri(source, UriKind.Relative) };
}
