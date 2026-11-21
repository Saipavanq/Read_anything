using System;
using System.Windows;

namespace ReadAloud.Services;

public sealed class ThemeService : IThemeService
{
    private const string AuroraSource = "Themes/Aurora.xaml";
    private const string OrbitSource = "Themes/Orbit.xaml";

    private readonly ResourceDictionary _applicationResources;
    private readonly Func<string, ResourceDictionary> _dictionaryFactory;

    public ThemeService(
        ResourceDictionary applicationResources,
        Func<string, ResourceDictionary>? dictionaryFactory = null)
    {
        _applicationResources = applicationResources;
        _dictionaryFactory = dictionaryFactory ?? DefaultDictionaryFactory;
    }

    public string CurrentTheme { get; private set; } = "aurora";

    public void Apply(string theme)
    {
        var normalized = string.Equals(theme, "orbit", StringComparison.OrdinalIgnoreCase)
            ? "orbit"
            : "aurora";

        var source = normalized == "orbit" ? OrbitSource : AuroraSource;
        var dictionary = _dictionaryFactory(source);

        if (_applicationResources.MergedDictionaries.Count > 0)
        {
            _applicationResources.MergedDictionaries[0] = dictionary;
        }
        else
        {
            _applicationResources.MergedDictionaries.Add(dictionary);
        }

        CurrentTheme = normalized;
    }

    private static ResourceDictionary DefaultDictionaryFactory(string source)
        => new() { Source = new Uri(source, UriKind.Relative) };
}
