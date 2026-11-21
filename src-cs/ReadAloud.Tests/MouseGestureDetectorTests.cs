using ReadAloud.Services;

namespace ReadAloud.Tests;

public sealed class MouseGestureDetectorTests
{
    [Fact]
    public void PlainClick_DoesNotTrigger()
    {
        var detector = new MouseGestureDetector();

        detector.OnDown(100, 100);
        detector.OnMove(101, 100);
        var triggered = detector.OnUp(101, 100, 1_000);

        Assert.False(triggered);
    }

    [Fact]
    public void DragSelection_TriggersOnMouseUp()
    {
        var detector = new MouseGestureDetector();

        detector.OnDown(100, 100);
        detector.OnMove(120, 130);
        var triggered = detector.OnUp(120, 130, 1_000);

        Assert.True(triggered);
    }

    [Fact]
    public void DoubleClick_TriggersOnSecondMouseUp()
    {
        var detector = new MouseGestureDetector();

        detector.OnDown(200, 200);
        Assert.False(detector.OnUp(200, 200, 1_000));

        detector.OnDown(202, 201);
        var triggered = detector.OnUp(202, 201, 1_200);

        Assert.True(triggered);
    }

    [Fact]
    public void SlowSecondClick_DoesNotTrigger()
    {
        var detector = new MouseGestureDetector();

        detector.OnDown(200, 200);
        Assert.False(detector.OnUp(200, 200, 1_000));

        detector.OnDown(202, 201);
        var triggered = detector.OnUp(202, 201, 2_000);

        Assert.False(triggered);
    }

    [Fact]
    public void DistantSecondClick_DoesNotTrigger()
    {
        var detector = new MouseGestureDetector();

        detector.OnDown(200, 200);
        Assert.False(detector.OnUp(200, 200, 1_000));

        detector.OnDown(400, 400);
        var triggered = detector.OnUp(400, 400, 1_200);

        Assert.False(triggered);
    }

    [Fact]
    public void Reset_ClearsPendingGestureState()
    {
        var detector = new MouseGestureDetector();
        detector.OnDown(200, 200);
        Assert.False(detector.OnUp(200, 200, 1_000));

        detector.Reset();

        detector.OnDown(202, 201);
        Assert.False(detector.OnUp(202, 201, 1_100));
    }
}
