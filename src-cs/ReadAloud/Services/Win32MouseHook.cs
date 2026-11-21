using System;
using System.Runtime.InteropServices;

namespace ReadAloud.Services;

public sealed class Win32MouseHook : IMouseHook, IDisposable
{
    private const int WhMouseLl = 14;
    private const int WmLButtonUp = 0x0202;

    private readonly HookProc _callback;
    private IntPtr _handle;

    public Win32MouseHook()
    {
        _callback = OnHookCallback;
    }

    public event Action? MouseUp;

    public void Start()
    {
        if (_handle != IntPtr.Zero)
        {
            return;
        }

        var module = GetModuleHandle(null);
        _handle = SetWindowsHookEx(WhMouseLl, _callback, module, 0);
    }

    public void Stop()
    {
        if (_handle == IntPtr.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(_handle);
        _handle = IntPtr.Zero;
    }

    public void Dispose() => Stop();

    private IntPtr OnHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WmLButtonUp)
        {
            MouseUp?.Invoke();
        }

        return CallNextHookEx(_handle, nCode, wParam, lParam);
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr handle);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr CallNextHookEx(IntPtr handle, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
