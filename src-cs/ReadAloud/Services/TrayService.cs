using System;

namespace ReadAloud.Services;

public sealed class TrayService
{
    private readonly Action _showWindow;
    private readonly Action _readSelection;
    private readonly Action _stop;
    private readonly Action _openSettings;
    private readonly Action _quit;

    public TrayService(
        Action showWindow,
        Action readSelection,
        Action stop,
        Action openSettings,
        Action quit)
    {
        _showWindow = showWindow ?? throw new ArgumentNullException(nameof(showWindow));
        _readSelection = readSelection ?? throw new ArgumentNullException(nameof(readSelection));
        _stop = stop ?? throw new ArgumentNullException(nameof(stop));
        _openSettings = openSettings ?? throw new ArgumentNullException(nameof(openSettings));
        _quit = quit ?? throw new ArgumentNullException(nameof(quit));

        MenuItems = new[]
        {
            "Show window",
            "Read selected text",
            "Stop speaking",
            "Settings…",
            "Quit"
        };
    }

    public IReadOnlyList<string> MenuItems { get; }

    public void ShowWindow() => _showWindow();

    public void ReadSelection() => _readSelection();

    public void Stop() => _stop();

    public void OpenSettings() => _openSettings();

    public void Quit() => _quit();
}
