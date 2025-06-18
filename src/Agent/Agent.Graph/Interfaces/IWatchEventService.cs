using Agent.Graph.Services;
using Azure.Core;
using Azure.ResourceManager.Monitor.Models;

namespace Agent.Graph.Interfaces;

public class WatchEvent
{
    public object EventData { get; set; }
}

public interface IWatchEventService
{
    public IAsyncEnumerable<WatchEvent> WatchEvents(List<WatchEventSource> sources, CancellationToken? cancellationToken = null);
}
