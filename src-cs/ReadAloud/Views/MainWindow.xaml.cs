using System;
using System.Windows;
using System.Windows.Input;
using ReadAloud.Models;
using ReadAloud.Services;
using ReadAloud.ViewModels;

namespace ReadAloud.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var themes = new ThemeService(Application.Current.Resources);
        themes.Apply("aurora");
        DataContext = new MainViewModel(themes, new PlaceholderHotkeyRegistrar());
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
