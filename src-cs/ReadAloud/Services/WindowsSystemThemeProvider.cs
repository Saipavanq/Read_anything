using System;
using Microsoft.Win32;

namespace ReadAloud.Services;

public sealed class WindowsSystemThemeProvider : ISystemThemeProvider
{
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightTheme = "AppsUseLightTheme";

    private bool _isDark;

    public WindowsSystemThemeProvider()
    {
        _isDark = ReadIsDark();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public bool IsDark => ReadIsDark();

    public event Action? Changed;

    public static bool IsDarkFromValue(object? value) => value is int number && number == 0;

    private static bool ReadIsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
            return IsDarkFromValue(key?.GetValue(AppsUseLightTheme));
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        var isDark = ReadIsDark();
        if (isDark == _isDark)
        {
            return;
        }

        _isDark = isDark;
        Changed?.Invoke();
    }
}
