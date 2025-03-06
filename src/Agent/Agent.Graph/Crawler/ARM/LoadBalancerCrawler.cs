using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Data.DatabaseManagers.GraphDatabase;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM
{
    public class LoadBalancerCrawler : GenericArmResourceCrawler
    {
        private readonly ILogger<LoadBalancerCrawler> _logger;
        private readonly IGraphDatabaseManager _dbManager;

        public LoadBalancerCrawler(ILogger<LoadBalancerCrawler> logger, IGraphDatabaseManager dbManager, ArmClient armClient)
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
}
