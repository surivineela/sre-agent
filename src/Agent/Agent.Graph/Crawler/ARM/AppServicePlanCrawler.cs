using Agent.Data.DatabaseClients.GraphDbClient;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;

namespace Agent.Graph.Crawler.ARM;

public class AppServicePlanCrawler : IResourceCrawler
{
    private readonly ILogger<AppServicePlanCrawler> _logger;
    private readonly IGraphDatabaseClient _dbGraphDbClient;

    public AppServicePlanCrawler(ILogger<AppServicePlanCrawler> logger, IGraphDatabaseClient dbGraphDbClient)
    {
        _logger = logger;
        _dbGraphDbClient = dbGraphDbClient;
    }

    public async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        var armNode = (ArmResourceNode)node;
        _logger.LogDebug($"Crawling App Service Plan {armNode.ResourceId}");

        // Simply add or update the node in the graph.
        await _dbGraphDbClient.AddOrUpdateNodeAsync(armNode.GetNodeLabel(), armNode.GetNodeId(), armNode.GetResourceType(), armNode.GetNodeProperties());
        Thread.Sleep(1000);
        yield break;
    }
}
