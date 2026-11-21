using ReadAloud.Models;

namespace ReadAloud.Services;

public interface IHistorySink
{
    void Record(HistoryEntry entry);
}
