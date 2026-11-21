using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ReadAloud.Models;
using ReadAloud.Services;

namespace ReadAloud.ViewModels;

public sealed class HistoryViewModel : ObservableObject
{
    private readonly HistoryService _history;
    private readonly ReadCoordinator _coordinator;
    private IReadOnlyList<HistoryEntry> _entries = Array.Empty<HistoryEntry>();

    public HistoryViewModel(HistoryService history, ReadCoordinator coordinator)
    {
        _history = history;
        _coordinator = coordinator;
        _history.Changed += Refresh;
        _entries = _history.Entries.ToList();

        ReplayCommand = new RelayCommand(entry =>
        {
            if (entry is HistoryEntry historyEntry)
            {
                _ = ReplayAsync(historyEntry);
            }
        });
        DeleteCommand = new RelayCommand(entry =>
        {
            if (entry is HistoryEntry historyEntry)
            {
                _history.Delete(historyEntry.Id);
                Refresh();
            }
        });
        DeleteAllCommand = new RelayCommand(_ =>
        {
            _history.DeleteAll();
            Refresh();
        });
    }

    public IReadOnlyList<HistoryEntry> Entries
    {
        get => _entries;
        private set => SetProperty(ref _entries, value);
    }

    public ICommand ReplayCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand DeleteAllCommand { get; }

    public async Task ReplayAsync(HistoryEntry entry)
    {
        if (entry is null)
        {
            return;
        }

        Enum.TryParse(entry.CaptureMethod, out CaptureMethod captureMethod);
        await _coordinator.RequestReadAsync(entry.Text, "replay", captureMethod);
        Refresh();
    }

    public void Refresh() => Entries = _history.Entries.ToList();
}
