using Agent.Data.DatabaseClients.GraphDbClient;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class AppServicePlanCrawler : IArmResourceCrawler
{
    private readonly ILogger<AppServicePlanCrawler> _logger;
    private readonly IGraphDatabaseClient _dbGraphDbClient;

    public AppServicePlanCrawler(ILogger<AppServicePlanCrawler> logger, IGraphDatabaseClient dbGraphDbClient)
    {
        _logger = logger;
        _dbGraphDbClient = dbGraphDbClient;
    }

    public async IAsyncEnumerable<ArmResourceNode> Crawl(ArmResourceNode node)
    {
        _logger.LogDebug($"Crawling App Service Plan {node.ResourceId}");

        // Simply add or update the node in the graph.
        await _dbGraphDbClient.AddOrUpdateNodeAsync(node.GetNodeLabel(), node.GetNodeId(), node.GetResourceType(), node.GetNodeProperties());
        Thread.Sleep(1000);
        yield break;
    }
}
