using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ReadAloud.Models;
using ReadAloud.Services;
using ReadAloud.ViewModels;

namespace ReadAloud.Tests;

public sealed class ThemeTests
{
    [Theory]
    [InlineData("dark", "aurora", "Themes/Dark.xaml", "Themes/Aurora.xaml")]
    [InlineData("light", "orbit", "Themes/Light.xaml", "Themes/Orbit.xaml")]
    public void Apply_LoadsBaseAccentAndControlsInOrder(
        string mode, string theme, string expectedBase, string expectedAccent)
    {
        var resources = new ResourceDictionary();
        var requested = new List<string>();
        var service = new ThemeService(resources, new FakeSystemThemeProvider(), source =>
        {
            requested.Add(source);
            return new ResourceDictionary();
        });

        service.Apply(mode, theme);

        Assert.Equal(new[] { expectedBase, expectedAccent, "Themes/Controls.xaml" }, requested);
        Assert.Equal(3, resources.MergedDictionaries.Count);
        Assert.Equal(mode, service.CurrentMode);
        Assert.Equal(theme, service.CurrentTheme);
    }

    [Theory]
    [InlineData(true, "Themes/Dark.xaml")]
    [InlineData(false, "Themes/Light.xaml")]
    public void Apply_System_UsesProviderValue(bool isDark, string expectedBase)
    {
        var requested = new List<string>();
        var service = new ThemeService(new ResourceDictionary(), new FakeSystemThemeProvider { IsDark = isDark }, source =>
        {
            requested.Add(source);
            return new ResourceDictionary();
        });

        service.Apply("system", "aurora");

        Assert.Equal(expectedBase, requested[0]);
        Assert.Equal("system", service.CurrentMode);
    }

    [Fact]
    public void SystemThemeChanged_IsIgnoredWhenModeIsFixed()
    {
        var provider = new FakeSystemThemeProvider { IsDark = false };
        var requested = new List<string>();
        var service = new ThemeService(new ResourceDictionary(), provider, source =>
        {
            requested.Add(source);
            return new ResourceDictionary();
        });

        service.Apply("dark", "aurora");
        requested.Clear();
        provider.IsDark = true;
        provider.RaiseChanged();

        Assert.Empty(requested);
        Assert.Equal("dark", service.CurrentMode);
    }

    [Fact]
    public void SystemThemeChanged_ReappliesWhenModeIsSystem()
    {
        var provider = new FakeSystemThemeProvider { IsDark = false };
        var requested = new List<string>();
        var service = new ThemeService(new ResourceDictionary(), provider, source =>
        {
            requested.Add(source);
            return new ResourceDictionary();
        });

        service.Apply("system", "aurora");
        requested.Clear();
        provider.IsDark = true;
        provider.RaiseChanged();

        Assert.Equal("Themes/Dark.xaml", requested[0]);
    }

    [Fact]
    public void InvalidModeAndTheme_FallBackToSystemAndAurora()
    {
        var requested = new List<string>();
        var service = new ThemeService(new ResourceDictionary(), new FakeSystemThemeProvider(), source =>
        {
            requested.Add(source);
            return new ResourceDictionary();
        });

        service.Apply("neon", "neon");

        Assert.Equal("system", service.CurrentMode);
        Assert.Equal("aurora", service.CurrentTheme);
        Assert.Equal("Themes/Aurora.xaml", requested[1]);
    }

    [Fact]
    public void WindowsSystemThemeProvider_IsDarkFromValue_HandlesMissingAndInvalid()
    {
        Assert.False(WindowsSystemThemeProvider.IsDarkFromValue(null));
        Assert.False(WindowsSystemThemeProvider.IsDarkFromValue(1));
        Assert.False(WindowsSystemThemeProvider.IsDarkFromValue("0"));
        Assert.True(WindowsSystemThemeProvider.IsDarkFromValue(0));
    }

    [Fact]
    public void MainViewModel_NavigateCommand_SelectsPage()
    {
        var model = new MainViewModel(new FakeThemeService(), new FakeHotkeyRegistrar());

        model.NavigateCommand.Execute("Voice Library");
        Assert.Equal("Voice Library", model.SelectedNavigation);

        model.NavigateCommand.Execute("History");
        Assert.Equal("History", model.SelectedNavigation);
    }

    [Fact]
    public void MainViewModel_MarksAllNavItemsEnabled()
    {
        var model = new MainViewModel(new FakeThemeService(), new FakeHotkeyRegistrar());

        Assert.Equal("Ready to listen?", model.Greeting);
        Assert.Equal("Ready", model.Status);
        Assert.Equal(
            new[] { "Home", "Voice Library", "History", "Settings" },
            model.NavigationItems);
        Assert.True(model.NavigationEnabled["Home"]);
        Assert.True(model.NavigationEnabled["Voice Library"]);
        Assert.True(model.NavigationEnabled["History"]);
        Assert.True(model.NavigationEnabled["Settings"]);

        model.ApplyTheme("dark", "orbit");
        Assert.Equal("dark", model.ColorMode);
        Assert.Equal("orbit", model.Theme);
    }

    private sealed class FakeSystemThemeProvider : ISystemThemeProvider
    {
        public bool IsDark { get; set; }

        public event Action? Changed;

        public void RaiseChanged() => Changed?.Invoke();
    }

    private sealed class FakeThemeService : IThemeService
    {
        public string CurrentTheme { get; private set; } = "aurora";

        public string CurrentMode { get; private set; } = "system";

        public void Apply(string mode, string theme)
        {
            CurrentMode = mode;
            CurrentTheme = theme;
        }
    }

    private sealed class FakeHotkeyRegistrar : IHotkeyRegistrar
    {
        public event Action<string>? HotkeyPressed;

        public bool TryRegister(HotkeyBinding binding) => true;

        public void UnregisterAll()
        {
        }
    }
}
