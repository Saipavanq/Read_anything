using System;
using System.Collections.Generic;
using System.Linq;

namespace ReadAloud.Services;

public sealed class OwnWindowForegroundPolicy
{
    private readonly Func<IntPtr> _foregroundWindow;
    private readonly Func<IEnumerable<IntPtr>> _ownWindows;

    public OwnWindowForegroundPolicy(
        Func<IntPtr> foregroundWindow,
        Func<IEnumerable<IntPtr>> ownWindows)
    {
        _foregroundWindow = foregroundWindow ?? throw new ArgumentNullException(nameof(foregroundWindow));
        _ownWindows = ownWindows ?? throw new ArgumentNullException(nameof(ownWindows));
    }

    public bool IsOwnWindowForeground()
    {
        var foreground = _foregroundWindow();
        return foreground != IntPtr.Zero && _ownWindows().Contains(foreground);
    }
}
