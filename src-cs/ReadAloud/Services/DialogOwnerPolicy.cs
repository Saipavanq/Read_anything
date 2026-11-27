using System;

namespace ReadAloud.Services;

public static class DialogOwnerPolicy
{
    public static bool ShouldAssignOwner(IntPtr mainWindowHandle)
        => mainWindowHandle != IntPtr.Zero;
}
