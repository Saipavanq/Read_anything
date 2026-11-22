namespace ReadAloud.Services;

public interface IThemeService
{
    string CurrentTheme { get; }

    string CurrentMode { get; }

    void Apply(string theme);

    void Apply(string mode, string theme);
}
