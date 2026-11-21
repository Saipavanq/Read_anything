using System;
using ReadAloud.Services;

namespace ReadAloud.Tests;

public sealed class ForegroundPolicyTests
{
    [Fact]
    public void OwnWindowHandle_IsForeground()
    {
        var policy = new OwnWindowForegroundPolicy(
            () => new IntPtr(20),
            () => new[] { new IntPtr(10), new IntPtr(20) });

        Assert.True(policy.IsOwnWindowForeground());
    }

    [Fact]
    public void OtherWindowHandle_IsNotForeground()
    {
        var policy = new OwnWindowForegroundPolicy(
            () => new IntPtr(99),
            () => new[] { new IntPtr(10), new IntPtr(20) });

        Assert.False(policy.IsOwnWindowForeground());
    }

    [Fact]
    public void NoForegroundWindow_IsNotForeground()
    {
        var policy = new OwnWindowForegroundPolicy(
            () => IntPtr.Zero,
            () => new[] { new IntPtr(10) });

        Assert.False(policy.IsOwnWindowForeground());
    }
}
