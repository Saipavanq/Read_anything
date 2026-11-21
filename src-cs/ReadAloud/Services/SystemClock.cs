namespace ReadAloud.Services;

internal sealed class SystemClock : IClock
{
    public Task Delay(TimeSpan delay) => Task.Delay(delay);
}
