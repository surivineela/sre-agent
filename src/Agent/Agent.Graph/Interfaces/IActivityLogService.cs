using Agent.Graph.Services;
using Azure.Core;
using Azure.ResourceManager.Monitor.Models;

namespace Agent.Graph.Interfaces;

public interface IActivityLogService
{
    public IAsyncEnumerable<EventDataInfo> WatchEvents(List<WatchEventSource> sources, CancellationToken? cancellationToken = null);
}
