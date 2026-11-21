using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using ReadAloud.Models;
using ReadAloud.Services;

namespace ReadAloud.Tests;

public sealed class HotkeyTests
{
    [Fact]
    public void Format_ProducesCtrlAltR()
    {
        var service = new HotkeyService(new FakeHotkeyRegistrar());

        var display = service.Format(ModifierKeys.Control | ModifierKeys.Alt, Key.R);

        Assert.Equal("Ctrl+Alt+R", display);
    }

    [Fact]
    public void DuplicateRecord_IsRejected()
    {
        var service = new HotkeyService(new FakeHotkeyRegistrar());

        Assert.True(service.TryRecord("read", ModifierKeys.Control | ModifierKeys.Alt, Key.R, out _, out _));
        var accepted = service.TryRecord("stop", ModifierKeys.Control | ModifierKeys.Alt, Key.R, out _, out var error);

        Assert.False(accepted);
        Assert.Contains("already used", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ModifierOnlyRecord_IsRejected()
    {
        var service = new HotkeyService(new FakeHotkeyRegistrar());

        var accepted = service.TryRecord("read", ModifierKeys.Control | ModifierKeys.Alt, Key.LeftCtrl, out _, out var error);

        Assert.False(accepted);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Arming_UnregistersGlobalHotkeys()
    {
        var registrar = new FakeHotkeyRegistrar();
        var service = new HotkeyService(registrar);

        service.BeginRecording();

        Assert.Equal(1, registrar.UnregisterCount);
    }

    [Fact]
    public void EndRecording_ReRegistersSavedConfig()
    {
        var registrar = new FakeHotkeyRegistrar();
        var service = new HotkeyService(registrar);
        var config = new AppConfig();

        service.BeginRecording();
        service.EndRecording(config);

        Assert.Equal(3, registrar.Registrations.Count);
        Assert.Contains("Ctrl+Alt+R", registrar.Registrations.Select(b => b.Display));
        Assert.Contains("Ctrl+Alt+X", registrar.Registrations.Select(b => b.Display));
    }

    [Fact]
    public void StartupConflict_SkipsOnlyConflictingAction()
    {
        var registrar = new FakeHotkeyRegistrar();
        registrar.Owned.Add("Ctrl+Alt+R");
        var service = new HotkeyService(registrar);
        var config = new AppConfig();

        var conflicts = service.RegisterAll(config, registrar.Owned);

        Assert.Single(conflicts);
        Assert.Contains("Ctrl+Alt+R", conflicts[0], StringComparison.Ordinal);
        var displays = registrar.Registrations.Select(b => b.Display).ToList();
        Assert.DoesNotContain("Ctrl+Alt+R", displays);
        Assert.Contains("Ctrl+Alt+X", displays);
    }

    private sealed class FakeHotkeyRegistrar : IHotkeyRegistrar
    {
        public event Action<string>? HotkeyPressed;

        public HashSet<string> Owned { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<HotkeyBinding> Registrations { get; } = new();
        public int UnregisterCount { get; private set; }

        public bool TryRegister(HotkeyBinding binding)
        {
            if (Owned.Contains(binding.Display))
            {
                return false;
            }

            Registrations.Add(binding);
            return true;
        }

        public void UnregisterAll()
        {
            UnregisterCount++;
            Registrations.Clear();
        }
    }
}
