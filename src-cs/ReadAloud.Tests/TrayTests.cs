using ReadAloud.Services;

namespace ReadAloud.Tests;

public sealed class TrayTests
{
    [Fact]
    public void TrayCommands_DelegateToExpectedActions()
    {
        var calls = new List<string>();
        var tray = new TrayService(
            () => calls.Add("show"),
            () => calls.Add("read"),
            () => calls.Add("stop"),
            () => calls.Add("settings"),
            () => calls.Add("quit"));

        Assert.Equal(
            new[] { "Show window", "Read selected text", "Stop speaking", "Settings…", "Quit" },
            tray.MenuItems);

        tray.ShowWindow();
        tray.ReadSelection();
        tray.Stop();
        tray.OpenSettings();
        tray.Quit();

        Assert.Equal(new[] { "show", "read", "stop", "settings", "quit" }, calls);
    }
}
