using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using ReadAloud.Models;
using ReadAloud.Services;
using ReadAloud.ViewModels;

namespace ReadAloud.Views;

public partial class MainWindow : Window
{
    private readonly ConfigService _configService;
    private readonly HotkeyService _hotkeys;
    private readonly IThemeService _themes;
    private readonly MainViewModel _main;

    public MainWindow()
    {
        InitializeComponent();

        _themes = new ThemeService(Application.Current.Resources);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _configService = new ConfigService(
            Path.Combine(appData, "Read Aloud", "settings.json"),
            Path.Combine(AppContext.BaseDirectory, "config", "settings.json"));

        var config = _configService.Load();
        _themes.Apply(config.Theme);

        var registrar = new PlaceholderHotkeyRegistrar();
        _hotkeys = new HotkeyService(registrar);
        _hotkeys.RegisterAll(config, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        _main = new MainViewModel(_themes, registrar);
        DataContext = _main;
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = new SettingsViewModel(_configService, _hotkeys, _themes, _main);
        new SettingsWindow(settings) { Owner = this }.ShowDialog();
    }

    private sealed class PlaceholderHotkeyRegistrar : IHotkeyRegistrar
    {
        public event Action<string>? HotkeyPressed;

        public bool TryRegister(HotkeyBinding binding) => true;

        public void UnregisterAll()
        {
        }
    }
}
