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
    [Fact]
    public void ApplyTheme_Orbit_SetsCurrentThemeAndMergedDictionary()
    {
        var resources = new ResourceDictionary();
        string? requestedSource = null;
        var service = new ThemeService(resources, source =>
        {
            requestedSource = source;
            return new ResourceDictionary();
        });

        service.Apply("orbit");

        Assert.Equal("orbit", service.CurrentTheme);
        Assert.Equal("Themes/Orbit.xaml", requestedSource);
        Assert.Single(resources.MergedDictionaries);
    }

    [Fact]
    public void UnknownTheme_FallsBackToAurora()
    {
        var resources = new ResourceDictionary();
        string? requestedSource = null;
        var service = new ThemeService(resources, source =>
        {
            requestedSource = source;
            return new ResourceDictionary();
        });

        service.Apply("dark");

        Assert.Equal("aurora", service.CurrentTheme);
        Assert.Equal("Themes/Aurora.xaml", requestedSource);
        Assert.Single(resources.MergedDictionaries);
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

        model.ApplyTheme("orbit");
        Assert.Equal("orbit", model.Theme);
    }

    private sealed class FakeThemeService : IThemeService
    {
        public string CurrentTheme { get; private set; } = "aurora";

        public void Apply(string theme) => CurrentTheme = theme == "orbit" ? "orbit" : "aurora";
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
