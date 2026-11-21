using System;
using System.Runtime.InteropServices;

namespace ReadAloud.Services;

public sealed class Win32MouseHook : IMouseHook, IDisposable
{
    private const int WhMouseLl = 14;
    private const int WmMouseMove = 0x0200;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonUp = 0x0202;

    private readonly HookProc _callback;
    private readonly MouseGestureDetector _gestures = new();
    private readonly Action<string>? _diagnostic;
    private IntPtr _handle;

    public Win32MouseHook(Action<string>? diagnostic = null)
    {
        _callback = OnHookCallback;
        _diagnostic = diagnostic;
    }

    public event Action? MouseUp;

    public void Start()
    {
        if (_handle != IntPtr.Zero)
        {
            return;
        }

        _gestures.Reset();
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
        _gestures.Reset();
    }

    public void Dispose() => Stop();

    private IntPtr OnHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            if (message is WmMouseMove or WmLButtonDown or WmLButtonUp)
            {
                var point = Marshal.PtrToStructure<MsllHookStruct>(lParam);
                var now = Environment.TickCount64;

                switch (message)
                {
                    case WmLButtonDown:
                        _gestures.OnDown(point.X, point.Y);
                        break;
                    case WmMouseMove:
                        _gestures.OnMove(point.X, point.Y);
                        break;
                    case WmLButtonUp:
                        if (_gestures.OnUp(point.X, point.Y, now))
                        {
                            _diagnostic?.Invoke("auto-read gesture detected");
                            MouseUp?.Invoke();
                        }

                        break;
                }
            }
        }

        return CallNextHookEx(_handle, nCode, wParam, lParam);
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MsllHookStruct
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

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
