// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Graph.Services;

namespace Agent.Graph.Interfaces;

public class WatchEvent
{
    public object EventData { get; set; }
}

public interface IWatchEventService
{
    public IAsyncEnumerable<WatchEvent> WatchEvents(List<WatchEventSource> sources, CancellationToken? cancellationToken = null);
}
