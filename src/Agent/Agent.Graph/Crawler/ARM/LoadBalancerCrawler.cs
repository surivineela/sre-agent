using Agent.Data.DatabaseClients.GraphDbClient;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class LoadBalancerCrawler : GenericArmResourceCrawler
{
    private readonly ILogger<LoadBalancerCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;

    public LoadBalancerCrawler(ILogger<LoadBalancerCrawler> logger, IGraphDatabaseClient graphDbClient, ArmClient armClient)
        : base(logger, graphDbClient, armClient)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
    }

    public override async IAsyncEnumerable<ArmResourceNode> Crawl(ArmResourceNode node)
    {
        await foreach (var n in base.Crawl(node))
        {
            yield return n;
        }
    }
}
