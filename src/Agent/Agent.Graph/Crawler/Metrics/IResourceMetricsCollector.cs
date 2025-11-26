// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient.Nodes;

namespace Agent.Graph.Crawler.Metrics;

public interface IResourceMetricsCollector
{
    public string ResourceType { get; }

    Task<AppHealthInfo> CollectMetricsAsync(GraphNode node);
}
