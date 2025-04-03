using Agent.Data.DatabaseClients.GraphDbClient;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.CosmosDB;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class CosmosDbCrawler : GenericArmResourceCrawler
{
    private readonly ILogger<CosmosDbCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;

    public CosmosDbCrawler(ILogger<CosmosDbCrawler> logger, IGraphDatabaseClient dbManager, ArmClient armClient)
        : base(logger, dbManager, armClient, false)
    {
        _logger = logger;
        _graphDbClient = dbManager;
    }

    public override async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        await foreach (var n in base.Crawl(node))
        {
            yield return n;
        }

        var cosmosDbNode = (CosmosDbNode)node;
        _logger.LogDebug($"Crawling Cosmos DB Account {cosmosDbNode.ResourceId}");

        var armResourceId = new ResourceIdentifier(cosmosDbNode.ResourceId);
        var resourceGroupId = ResourceGroupResource.CreateResourceIdentifier(armResourceId.SubscriptionId, armResourceId.ResourceGroupName);
        var resourceGroup = _armClient.GetResourceGroupResource(resourceGroupId);
        var cosmosDbResponse = await resourceGroup.GetCosmosDBAccountAsync(armResourceId.Name);
        var cosmosDbAccount = cosmosDbResponse.Value;

        cosmosDbNode.ConsistencyPolicy = cosmosDbAccount.Data.ConsistencyPolicy?.DefaultConsistencyLevel.ToString();

        await _graphDbClient.AddOrUpdateNodeAsync(cosmosDbNode);

        // Crawl databases within the Cosmos DB account
        await foreach (var database in cosmosDbAccount.GetCosmosDBSqlDatabases().GetAllAsync())
        {
            var databaseNode = new ArmResourceNode(
                resourceType: "Microsoft.DocumentDB/databaseAccounts/sqlDatabases",
                resourceId: database.Id,
                subscriptionId: armResourceId.SubscriptionId,
                resourceGroupName: armResourceId.ResourceGroupName,
                resourceName: database.Data.Name);

            await _graphDbClient.AddOrUpdateNodeAsync(databaseNode);

            var edge = new ArmResourceEdge(cosmosDbNode.GetNodeId(), databaseNode.GetNodeId(), Constants.Relationships.Contains);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);

            _logger.LogDebug($"Linked Cosmos DB Account {cosmosDbNode.ResourceId} with Sql Database {database.Data.Name}");

            yield return databaseNode;
        }
    }
}
