using System.Collections.Concurrent;

namespace Agent.Logging;

public class LogBuffer
{
    public ConcurrentQueue<object> Logs { get; } = new();
}
