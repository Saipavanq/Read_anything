using System;
using System.Threading.Tasks;

namespace ReadAloud.Services;

public sealed class SelectionReadFlow
{
    private readonly Func<Task<bool>> _read;
    private readonly Action _showMiniPlayer;

    public SelectionReadFlow(Func<Task<bool>> read, Action showMiniPlayer)
    {
        _read = read ?? throw new ArgumentNullException(nameof(read));
        _showMiniPlayer = showMiniPlayer ?? throw new ArgumentNullException(nameof(showMiniPlayer));
    }

    public async Task RunAsync()
    {
        if (await _read())
        {
            _showMiniPlayer();
        }
    }
}
