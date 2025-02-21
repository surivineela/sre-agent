using Agent.Data.DatabaseManagers.GraphDatabase;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM
{
    public class VirtualNetworkCrawler : GenericArmResourceCrawler
    {
        private readonly ILogger<VirtualNetworkCrawler> _logger;
        private readonly IGraphDatabaseManager _dbManager;
        private readonly ArmClient _armClient;

        public VirtualNetworkCrawler(ILogger<VirtualNetworkCrawler> logger, IGraphDatabaseManager dbManager)
            : base(logger, dbManager)
        {
            _logger = logger;
            _dbManager = dbManager;
            _armClient = new ArmClient(new DefaultAzureCredential());
        }

        public override async IAsyncEnumerable<ArmResourceNode> Crawl(ArmResourceNode node)
        {
            await foreach (var n in base.Crawl(node))
            {
                yield return n;
            }
        }
    }
}
