using System;
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

    protected override void OnStateChanged(EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _minimizeToTray())
        {
            Hide();
        }

        base.OnStateChanged(e);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => _openSettings();

    private void HideButton_Click(object sender, RoutedEventArgs e) => Hide();
}
