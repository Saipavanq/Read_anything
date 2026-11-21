using System;

namespace ReadAloud.Models;

public enum PlaybackStatus
{
    Stopped,
    Playing,
    Paused,
    Completed
}

public sealed record PlaybackState(
    string Text,
    IReadOnlyList<string> Sentences,
    int CurrentSentenceIndex,
    PlaybackStatus Status,
    double Speed,
    string VoiceName,
    TimeSpan Elapsed,
    TimeSpan EstimatedRemaining,
    double Progress);
