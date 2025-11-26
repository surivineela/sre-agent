// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Agent.Graph.Interfaces;

namespace Agent.Graph.Crawler.Kubernetes;

// literally do nothing
public class KubernetesDummyCrawler : IResourceCrawler
{
    public async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        await Task.Yield();
        yield break;
    }
}

