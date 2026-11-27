using System;
using System.ComponentModel;
using System.Windows;
using ReadAloud.ViewModels;

namespace ReadAloud.Views;

public partial class MainWindow : Window
{
    private readonly Action _openSettings;
    private readonly Func<bool> _minimizeToTray;
    private readonly Func<bool> _runInBackground;

    public MainWindow(
        MainViewModel main,
        Action openSettings,
        Func<bool> minimizeToTray,
        Func<bool> runInBackground)
    {
        InitializeComponent();
        DataContext = main;
        _openSettings = openSettings;
        _minimizeToTray = minimizeToTray;
        _runInBackground = runInBackground;
    }

    public static bool ShouldHideOnMinimize(WindowState state, bool minimizeToTray)
        => minimizeToTray && state == WindowState.Minimized;

    public static bool ShouldExitOnClose(bool runInBackground)
        => !runInBackground;

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
        if (!ShouldExitOnClose(_runInBackground()))
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
