using System;

namespace ReadAloud.Models;

public sealed record HistoryEntry(
    string Id,
    string Text,
    DateTimeOffset Timestamp,
    TimeSpan Duration,
    string Status,
    string Voice,
    double Speed,
    string CaptureMethod,
    string Source);
