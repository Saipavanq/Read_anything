using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using ReadAloud.Models;

namespace ReadAloud.Services;

internal sealed class Win32HotkeyRegistrar : IHotkeyRegistrar, IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    private readonly object _gate = new();
    private readonly Dictionary<int, HotkeyBinding> _bindings = new();
    private HwndSource? _source;
    private int _nextId = 1;
    private bool _disposed;

    public event Action<string>? HotkeyPressed;

    public bool TryRegister(HotkeyBinding binding)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var handle = EnsureWindow();
        var id = _nextId++;
        var modifiers = ToNativeModifiers(binding.Modifiers) | ModNoRepeat;
        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(binding.Key);

        if (virtualKey == 0 || !RegisterHotKey(handle, id, modifiers, virtualKey))
        {
            return false;
        }

        lock (_gate)
        {
            _bindings[id] = binding;
        }

        return true;
    }

    public void UnregisterAll()
    {
        if (_source is null)
        {
            return;
        }

        int[] ids;
        lock (_gate)
        {
            ids = _bindings.Keys.ToArray();
            _bindings.Clear();
        }

        foreach (var id in ids)
        {
            UnregisterHotKey(_source.Handle, id);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        UnregisterAll();
        _source?.RemoveHook(WndProc);
        _source?.Dispose();
        _source = null;
        _disposed = true;
    }

    private IntPtr EnsureWindow()
    {
        if (_source is not null)
        {
            return _source.Handle;
        }

        var parameters = new HwndSourceParameters("ReadAloudHotkeys", 0, 0)
        {
            WindowStyle = 0,
            ParentWindow = new IntPtr(-3)
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
        return _source.Handle;
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WmHotkey)
        {
            return IntPtr.Zero;
        }

        HotkeyBinding? binding;
        lock (_gate)
        {
            _bindings.TryGetValue(wParam.ToInt32(), out binding);
        }

        if (binding is null)
        {
            return IntPtr.Zero;
        }

        HotkeyPressed?.Invoke(binding.Action);
        handled = true;
        return IntPtr.Zero;
    }

    private static uint ToNativeModifiers(ModifierKeys modifiers)
    {
        var value = 0u;

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            value |= ModAlt;
        }

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            value |= ModControl;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            value |= ModShift;
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            value |= ModWin;
        }

        return value;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
