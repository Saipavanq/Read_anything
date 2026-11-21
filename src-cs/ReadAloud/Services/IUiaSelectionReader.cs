using System;
using System.Threading.Tasks;

namespace ReadAloud.Services;

public interface IUiaSelectionReader
{
    Task<string?> TryReadSelectionAsync(TimeSpan timeout);
}
