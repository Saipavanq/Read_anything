using System;
using System.Threading.Tasks;

namespace ReadAloud.Services;

public interface IClock
{
    Task Delay(TimeSpan delay);
}
