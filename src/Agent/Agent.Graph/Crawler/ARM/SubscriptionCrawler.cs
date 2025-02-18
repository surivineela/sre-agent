using System.Text.Json;
using Agent.Data.DatabaseManagers.GraphDatabase;
using Agent.Graph.Schema;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;
using static System.Net.Mime.MediaTypeNames;

namespace Agent.Graph.Crawler.ARM;

public class SubscriptionCrawler : IArmResourceCrawler
{
    private readonly ILogger<SubscriptionCrawler> _logger;
    private readonly IGraphDatabaseManager _dbManager;
    private readonly ArmClient _armClient;
    private readonly AzureResourceGraphClient _graphClient;

    public SubscriptionCrawler(ILogger<SubscriptionCrawler> logger, IGraphDatabaseManager dbManager)
    {
        _logger = logger;
        _dbManager = dbManager;
        _armClient = new ArmClient(new DefaultAzureCredential());
        _graphClient = new AzureResourceGraphClient();
        _graphClient.InitTenantResource("72f988bf-86f1-41af-91ab-2d7cd011db47");
    }
    public static async Task<List<Node>> CrawlAllSubscriptions(InMemoryGraphManager inMemoryGraphManager)
    {
        string[] displayNameFilter = ["Container Apps Test Resources", "ruslany", "sanmeht", "yanche", "shgup", "pbatum"];

        var subscriptionList = new List<Node>();
        // Authenticate using DefaultAzureCredential
        var credential = new DefaultAzureCredential();
        // Create an instance of the ArmClient to interact with Azure
        var armClient = new ArmClient(credential);
        // Get all subscriptions
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
        _logger.LogInformation($"Crawling subscription {node.SubscriptionId}");
        await _dbManager.AddOrUpdateNodeAsync(node.GetNodeLabel(), node.GetNodeId(), node.GetResourceType(), node.GetNodeProperties());

        // get all container app environments
        var queryResult = await _graphClient.Query([node.SubscriptionId], $"resources|where type =~ 'Microsoft.App/managedEnvironments'| project id, type, subscriptionId, resourceGroup, name, location");
        _logger.LogInformation($"Find {queryResult.Count} container app environments under {node.SubscriptionId}");
        var jsonObj = JsonSerializer.Deserialize<JsonElement>(queryResult.Data);
        foreach (var item in jsonObj.EnumerateArray())
        {
            var resourceId = item.GetProperty("id").GetString();
            var resourceType = item.GetProperty("type").GetString();
            var subscriptionId = item.GetProperty("subscriptionId").GetString();
            var resourceGroupName = item.GetProperty("resourceGroup").GetString();
            var resourceName = item.GetProperty("name").GetString();
            var location = item.GetProperty("location").GetString();
            var envNode = new ContainerAppEnvironmentNode(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location);

            await _dbManager.AddOrUpdateNodeAsync(envNode.GetNodeLabel(), envNode.GetNodeId(), envNode.GetResourceType(), envNode.GetNodeProperties());
            await _dbManager.AddEdgeIfNotExistsAsync(node.GetNodeId(), envNode.GetNodeId(), "CONTAINS");
            yield return envNode;
        }
    }
}
