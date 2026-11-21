using System;
using System.Collections.ObjectModel;

namespace ReadAloud.Services;

public sealed class ActivityLog
{
    public ObservableCollection<string> Entries { get; } = new();

    public void Add(string message)
        => Entries.Add($"{DateTime.Now:HH:mm:ss} {message}");
}
