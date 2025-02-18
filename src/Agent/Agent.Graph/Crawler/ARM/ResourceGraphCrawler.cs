using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Data.DatabaseManagers.GraphDatabase;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM
{
    public class ResourceGraphCrawler
    {
        private readonly ILogger<ResourceGraphCrawler> _logger;
        private readonly ArmResourceCrawlerFactory _factory;
        private readonly IGraphDatabaseManager _dbManager;

        public ResourceGraphCrawler(ILogger<ResourceGraphCrawler> logger, ArmResourceCrawlerFactory factory, IGraphDatabaseManager dbManager)
        {
            _logger = logger;
            _factory = factory;
            _dbManager = dbManager;
        }

        public async Task Crawl(IList<ArmResourceNode> nodes)
        {
            HashSet<string> crawled = new();
            Queue<ArmResourceNode> toCrawl = new();

            foreach (var node in nodes)
            {
                toCrawl.Enqueue(node);
            }

            while (toCrawl.TryDequeue(out var node))
            {
                if (crawled.Contains(node.ResourceId))
                {
                    continue;
                }
                crawled.Add(node.ResourceId);
                var crawler = _factory.CreateFromNode(node, _dbManager);
                await foreach(var n in crawler.Crawl(node))
                {
                    toCrawl.Enqueue(n);
                }
            }
        }
    }
}
