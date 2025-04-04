// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class AppServicePlanCrawler : GenericArmResourceCrawler
{
    private readonly ILogger<AppServicePlanCrawler> _logger;
    private readonly IGraphDatabaseClient _dbGraphDbClient;

    public AppServicePlanCrawler(ILogger<AppServicePlanCrawler> logger, IGraphDatabaseClient dbGraphDbClient, ArmClient client)
        : base(logger, dbGraphDbClient, client)
    {
        _logger = logger;
        _dbGraphDbClient = dbGraphDbClient;
    }

    public async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        await foreach (var n in base.Crawl(node))
        {
            yield return n;
        }

        var armNode = (ArmResourceNode)node;
        _logger.LogDebug($"Crawling App Service Plan {armNode.ResourceId}");

        // Simply add or update the node in the graph.
        await _dbGraphDbClient.AddOrUpdateNodeAsync(armNode);
        yield break;
    }
}

