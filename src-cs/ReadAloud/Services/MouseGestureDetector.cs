using System;

namespace ReadAloud.Services;

/// <summary>
/// Recognises the two gestures that mean "the user selected text": a drag and a
/// double-click. Coordinates live only in these transient fields; they are never
/// logged, persisted or exposed outside a single gesture.
/// </summary>
public sealed class MouseGestureDetector
{
    public const int DragThresholdPixels = 8;
    public const int DoubleClickMaxMilliseconds = 500;
    public const int DoubleClickMaxDistancePixels = 6;

    private int _downX;
    private int _downY;
    private bool _isDown;
    private bool _dragged;
    private int _lastUpX;
    private int _lastUpY;
    private long _lastUpTime;
    private bool _hasLastUp;

    public void OnDown(int x, int y)
    {
        _isDown = true;
        _dragged = false;
        _downX = x;
        _downY = y;
    }

    public void OnMove(int x, int y)
    {
        if (_isDown && Distance(x, y, _downX, _downY) > DragThresholdPixels)
        {
            _dragged = true;
        }
    }

    public bool OnUp(int x, int y, long nowMilliseconds)
    {
        var isDrag = _isDown && _dragged;
        var isDoubleClick = _hasLastUp
            && nowMilliseconds - _lastUpTime <= DoubleClickMaxMilliseconds
            && Distance(x, y, _lastUpX, _lastUpY) <= DoubleClickMaxDistancePixels;

        _isDown = false;
        _dragged = false;

        if (isDrag || isDoubleClick)
        {
            // Consume the gesture so a triple-click cannot fire twice.
            _hasLastUp = false;
            return true;
        }

        _lastUpX = x;
        _lastUpY = y;
        _lastUpTime = nowMilliseconds;
        _hasLastUp = true;
        return false;
    }

    public void Reset()
    {
        _isDown = false;
        _dragged = false;
        _hasLastUp = false;
    }

    private static int Distance(int x1, int y1, int x2, int y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return (int)Math.Sqrt((dx * (double)dx) + (dy * (double)dy));
    }
}
