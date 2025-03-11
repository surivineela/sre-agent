using Agent.Data.DatabaseManagers.GraphDatabase;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class VirtualNetworkCrawler : GenericArmResourceCrawler
{
    private readonly ILogger<VirtualNetworkCrawler> _logger;
    private readonly IGraphDatabaseManager _dbManager;

    public VirtualNetworkCrawler(ILogger<VirtualNetworkCrawler> logger, IGraphDatabaseManager dbManager, ArmClient armClient)
        : base(logger, dbManager, armClient)
    {
        _logger = logger;
        _dbManager = dbManager;
    }

    public override async IAsyncEnumerable<ArmResourceNode> Crawl(ArmResourceNode node)
    {
        await foreach (var n in base.Crawl(node))
        {
            yield return n;
        }
    }
}
