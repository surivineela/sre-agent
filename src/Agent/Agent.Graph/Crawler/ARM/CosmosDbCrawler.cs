// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.CosmosDB;
using Azure.ResourceManager.CosmosDB.Models;
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
        _logger.LogInternalInformation($"Crawling Cosmos DB Account {cosmosDbNode.ResourceId}");

        var armResourceId = new ResourceIdentifier(cosmosDbNode.ResourceId);
        var resourceGroupId = ResourceGroupResource.CreateResourceIdentifier(armResourceId.SubscriptionId, armResourceId.ResourceGroupName);
        var resourceGroup = _armClient.GetResourceGroupResource(resourceGroupId);
        var cosmosDbResponse = await resourceGroup.GetCosmosDBAccountAsync(armResourceId.Name);
        var cosmosDbAccount = cosmosDbResponse.Value;

        cosmosDbNode.ConsistencyPolicy = cosmosDbAccount.Data.ConsistencyPolicy?.DefaultConsistencyLevel.ToString();
        cosmosDbNode.ProvisioningState = cosmosDbAccount.Data.ProvisioningState;

        cosmosDbNode.ConsistencyPolicy = cosmosDbAccount.Data.ConsistencyPolicy?.DefaultConsistencyLevel.ToString();
        cosmosDbNode.ProvisioningState = cosmosDbAccount.Data.ProvisioningState;
        cosmosDbNode.MinimalTlsVersion = cosmosDbAccount.Data.MinimalTlsVersion.ToString();

        cosmosDbNode.WriteLocations = SerializeLocations(cosmosDbAccount.Data.WriteLocations);
        cosmosDbNode.ReadLocations = SerializeLocations(cosmosDbAccount.Data.ReadLocations);
        cosmosDbNode.IPRules = SerializeIPRules(cosmosDbAccount.Data.IPRules);

        cosmosDbNode.PublicNetworkAccess = cosmosDbAccount.Data.PublicNetworkAccess?.ToString();
        cosmosDbNode.BackupPolicy = cosmosDbAccount.Data.BackupPolicy?.MigrationState.ToString();
        cosmosDbNode.DocumentEndpoint = cosmosDbAccount.Data.DocumentEndpoint;
        cosmosDbNode.EnableAutomaticFailover = cosmosDbAccount.Data.EnableAutomaticFailover.ToString();

        await _graphDbClient.AddOrUpdateNodeAsync(cosmosDbNode);

        // Crawl databases within the Cosmos DB account
        await foreach (var database in cosmosDbAccount.GetCosmosDBSqlDatabases().GetAllAsync())
        {
            var databaseNode = new ArmResourceNode(
                resourceType: "Microsoft.DocumentDB/databaseAccounts/sqlDatabases",
                resourceId: database.Id!,
                subscriptionId: armResourceId.SubscriptionId!,
                resourceGroupName: armResourceId.ResourceGroupName!,
                resourceName: database.Data.Name);

            await _graphDbClient.AddOrUpdateNodeAsync(databaseNode);

            var edge = new ArmResourceEdge(cosmosDbNode.GetNodeId(), databaseNode.GetNodeId(), Constants.Relationships.Contains);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);

            _logger.LogDebug($"Linked Cosmos DB Account {cosmosDbNode.ResourceId} with Sql Database {database.Data.Name}");

            yield return databaseNode;
        }
    }

    private string? SerializeLocations(IReadOnlyList<CosmosDBAccountLocation> locations)
    {
        if (locations == null || locations.Count == 0)
        {
            return null;
        }

        return string.Join(",", locations.Select(l => l.LocationName));
    }

    private string? SerializeIPRules(IList<CosmosDBIPAddressOrRange> rules)
    {
        if (rules == null || rules.Count == 0)
        {
            return null;
        }

        return string.Join(",", rules.Select(r => r.IPAddressOrRange));
    }
}

