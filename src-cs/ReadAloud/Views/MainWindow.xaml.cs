using System;
using System.ComponentModel;
using System.Windows;
using ReadAloud.ViewModels;

namespace ReadAloud.Views;

public partial class MainWindow : Window
{
    private readonly Action _openSettings;
    private readonly Func<bool> _minimizeToTray;

    public MainWindow(MainViewModel main, Action openSettings, Func<bool> minimizeToTray)
    {
        InitializeComponent();
        DataContext = main;
        _openSettings = openSettings;
        _minimizeToTray = minimizeToTray;
    }

    public static bool ShouldHideOnMinimize(WindowState state, bool minimizeToTray)
        => minimizeToTray && state == WindowState.Minimized;

    public static bool ShouldExitOnClose(bool minimizeToTray)
    {
        _ = minimizeToTray;
        return true;
    }

    protected override void OnStateChanged(EventArgs e)
    {
        if (ShouldHideOnMinimize(WindowState, _minimizeToTray()))
        {
            Hide();
        }

        base.OnStateChanged(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!ShouldExitOnClose(_minimizeToTray()))
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => _openSettings();

    private void HideButton_Click(object sender, RoutedEventArgs e) => Hide();
}
