// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient.Nodes;

namespace Agent.Graph.Interfaces;

public interface IResourceCrawler
{
    public IAsyncEnumerable<GraphNode> Crawl(GraphNode node);
}

