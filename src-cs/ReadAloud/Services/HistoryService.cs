using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReadAloud.Models;

namespace ReadAloud.Services;

public sealed class HistoryService : IHistorySink
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private readonly string _path;
    private List<HistoryEntry> _entries = new();
    private bool _enabled;

    public HistoryService(string path)
    {
        _path = path;
        Load();
    }

    public event Action? Changed;

    public bool IsEnabled => _enabled;

    public IReadOnlyList<HistoryEntry> Entries => _entries.AsReadOnly();

    public void SetEnabled(bool enabled)
    {
        if (_enabled == enabled)
        {
            return;
        }

        _enabled = enabled;
        Save();
        Changed?.Invoke();
    }

    public void Add(HistoryEntry entry) => Record(entry);

    public void Record(HistoryEntry entry)
    {
        if (!_enabled || entry is null)
        {
            return;
        }

        var index = _entries.FindIndex(existing => existing.Id == entry.Id);
        if (index >= 0)
        {
            _entries[index] = entry;
        }
        else
        {
            _entries.Add(entry);
        }

        _entries = _entries
            .OrderByDescending(item => item.Timestamp)
            .ToList();
        Save();
        Changed?.Invoke();
    }

    public void Delete(string id)
    {
        if (_entries.RemoveAll(entry => entry.Id == id) == 0)
        {
            return;
        }

        Save();
        Changed?.Invoke();
    }

    public void DeleteAll()
    {
        if (_entries.Count == 0)
        {
            return;
        }

        _entries.Clear();
        Save();
        Changed?.Invoke();
    }

    public void ApplyRetention(int days)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Max(1, days));
        var removed = _entries.RemoveAll(entry => entry.Timestamp < cutoff);
        if (removed == 0)
        {
            return;
        }

        Save();
        Changed?.Invoke();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return;
            }

            var document = JsonSerializer.Deserialize<HistoryDocument>(File.ReadAllText(_path), SerializerOptions);
            _enabled = document?.Enabled ?? false;
            _entries = document?.Entries is null
                ? new List<HistoryEntry>()
                : document.Entries
                    .Where(entry => entry is not null && !string.IsNullOrWhiteSpace(entry.Id))
                    .OrderByDescending(entry => entry.Timestamp)
                    .ToList();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            _enabled = false;
            _entries = new List<HistoryEntry>();
        }
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var document = new HistoryDocument { Enabled = _enabled, Entries = _entries };
            File.WriteAllText(_path, JsonSerializer.Serialize(document, SerializerOptions));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // History is best-effort; a failed write must never break reading aloud.
        }
    }

    private sealed class HistoryDocument
    {
        public bool Enabled { get; set; }

        public List<HistoryEntry> Entries { get; set; } = new();
    }
}
