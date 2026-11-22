namespace ReadAloud.Services;

public interface ISystemThemeProvider
{
    bool IsDark { get; }

    event Action? Changed;
}
