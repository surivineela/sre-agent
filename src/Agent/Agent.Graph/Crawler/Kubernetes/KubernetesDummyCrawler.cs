// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;

namespace Agent.Graph.Crawler.ARM;

// literally do nothing
public class KubernetesDummyCrawler : IResourceCrawler
{
    public async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        yield break;
    }
}

