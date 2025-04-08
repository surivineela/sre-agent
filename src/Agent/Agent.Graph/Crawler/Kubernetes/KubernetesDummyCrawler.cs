// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Azure.Core;
using k8s;
using k8s.Models;

namespace Agent.Graph.Crawler.Kubernetes;

// literally do nothing
public class KubernetesDummyCrawler : IResourceCrawler
{
    public async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        yield break;
    }
}

