// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class VirtualNetworkCrawler : GenericArmResourceCrawler
{
    private readonly ILogger<VirtualNetworkCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;

    public VirtualNetworkCrawler(ILogger<VirtualNetworkCrawler> logger, IGraphDatabaseClient graphDbClient, ArmClient armClient)
        : base(logger, graphDbClient, armClient)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
    }

    public override async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        await foreach (var n in base.Crawl(node))
        {
            yield return n;
        }
    }
}
