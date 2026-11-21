namespace ReadAloud.Services;

public interface IThemeService
{
    string CurrentTheme { get; }

    void Apply(string theme);
}
