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
        private readonly AzureResourceGraphClient _graphClient;

        public ResourceGraphCrawler(ILogger<ResourceGraphCrawler> logger, ArmResourceCrawlerFactory factory, IGraphDatabaseManager dbManager, AzureResourceGraphClient graphClient)
        {
            _logger = logger;
            _factory = factory;
            _dbManager = dbManager;
            _graphClient = graphClient;
        }

        public async Task Crawl(IList<ArmResourceNode> nodes, CancellationToken? cancellationToken = null)
        {
            try
            {
                HashSet<string> crawled = new();
                Queue<ArmResourceNode> toCrawl = new();

                foreach (var node in nodes)
                {
                    toCrawl.Enqueue(node);
                }

                while (toCrawl.TryDequeue(out var node))
                {
                    if (crawled.Contains(node.GetHashString()))
                    {
                        continue;
                    }
                    crawled.Add(node.GetHashString());
                    var crawler = _factory.CreateFromNode(node, _dbManager, _graphClient);
                    await foreach(var n in crawler.Crawl(node))
                    {
                        toCrawl.Enqueue(n);
                    }
                }
                _logger.LogDebug($"Done crawling");
            } catch (Exception ex)
            {
                _logger.LogError(ex, "Error crawling resources");
            }
        }
    }
}
