using Agent.Data.DatabaseManagers.GraphDatabase;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class AppServicePlanCrawler : IArmResourceCrawler
{
    private readonly ILogger<AppServicePlanCrawler> _logger;
    private readonly IGraphDatabaseManager _dbManager;

    public AppServicePlanCrawler(ILogger<AppServicePlanCrawler> logger, IGraphDatabaseManager dbManager)
    {
        _logger = logger;
        _dbManager = dbManager;
    }

    public async IAsyncEnumerable<ArmResourceNode> Crawl(ArmResourceNode node)
    {
        _logger.LogDebug($"Crawling App Service Plan {node.ResourceId}");

        // Simply add or update the node in the graph.
        await _dbManager.AddOrUpdateNodeAsync(node.GetNodeLabel(), node.GetNodeId(), node.GetResourceType(), node.GetNodeProperties());
        Thread.Sleep(1000);
        yield break;
    }
}
