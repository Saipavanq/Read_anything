using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ReadAloud.Services;

namespace ReadAloud.Tests;

public sealed class SelectionFlowTests
{
    [Fact]
    public async Task RunAsync_ReadsBeforeShowingMiniPlayer()
    {
        var events = new List<string>();
        var flow = new SelectionReadFlow(
            async () =>
            {
                await Task.Yield();
                events.Add("read");
                return true;
            },
            () => events.Add("show"));

        await flow.RunAsync();

        Assert.Equal(new[] { "read", "show" }, events);
    }

    [Fact]
    public async Task RunAsync_FailedCapture_DoesNotShowMiniPlayer()
    {
        var shown = false;
        var flow = new SelectionReadFlow(
            () => Task.FromResult(false),
            () => shown = true);

        await flow.RunAsync();

        Assert.False(shown);
    }

    [Fact]
    public async Task RunAsync_ReadFailure_DoesNotShowMiniPlayer()
    {
        var shown = false;
        var flow = new SelectionReadFlow(
            () => Task.FromException<bool>(new InvalidOperationException("read failed")),
            () => shown = true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => flow.RunAsync());

        Assert.False(shown);
    }
}
