
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Agent.Data.DatabaseManagers.GraphDatabase;
using Agent.Graph.Schema;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM
{
    public class SubscriptionCrawler : IArmResourceCrawler
    {
        private readonly ILogger<SubscriptionCrawler> _logger;
        private readonly IGraphDatabaseManager _dbManager;
        private readonly ArmClient _armClient;

        public SubscriptionCrawler(ILogger<SubscriptionCrawler> logger, IGraphDatabaseManager dbManager, ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _dbManager = dbManager;
            _armClient = new ArmClient(new DefaultAzureCredential());
        }

        public static async Task<List<Node>> CrawlAllSubscriptions(InMemoryGraphManager inMemoryGraphManager)
        {
            string[] displayNameFilter = ["Container Apps Test Resources", "ruslany", "sanmeht", "yanche", "shgup", "pbatum"];

            var subscriptionList = new List<Node>();
            var credential = new DefaultAzureCredential();
            var armClient = new ArmClient(credential);
            await foreach (var subscription in armClient.GetSubscriptions().GetAllAsync())
            {
                if (displayNameFilter != null && displayNameFilter.Length > 0 &&
                    !displayNameFilter.Any(filter => subscription.Data.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var subscriptionNode = new Node(
                    id: subscription.Data.Id,
                    name: subscription.Data.DisplayName,
                    type: "Subscription");
                inMemoryGraphManager.AddOrUpdateNode(subscriptionNode);
                subscriptionList.Add(subscriptionNode);
            }

            return subscriptionList;
        }

        public async IAsyncEnumerable<ArmResourceNode> Crawl(ArmResourceNode node)
        {
            var subNode = (SubscriptionNode)node;
            _logger.LogDebug($"Crawling for subscription {subNode.SubscriptionId}");
            await _dbManager.AddOrUpdateNodeAsync(subNode.GetNodeLabel(), subNode.GetNodeId(), subNode.GetResourceType(), subNode.GetNodeProperties());

            var subArmId = SubscriptionResource.CreateResourceIdentifier(subNode.SubscriptionId);
            var subResource = _armClient.GetSubscriptionResource(subArmId);

            await foreach (var rg in subResource.GetResourceGroups().GetAllAsync())
            {
                var rgNode = new ResourceGroupNode(subNode.SubscriptionId, rg.Data.Name);
                await _dbManager.AddOrUpdateNodeAsync(rgNode.GetNodeLabel(), rgNode.GetNodeId(), rgNode.GetResourceType(), rgNode.GetNodeProperties());
                await _dbManager.AddEdgeIfNotExistsAsync(subNode.GetNodeId(), rgNode.GetNodeId(), "CONTAINS");

                yield return rgNode;
            }
        }
    }
}
