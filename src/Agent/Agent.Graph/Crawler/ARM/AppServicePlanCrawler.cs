// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class AppServicePlanCrawler : GenericArmResourceCrawler
{
    private readonly ILogger<AppServicePlanCrawler> _logger;
    private readonly IGraphDatabaseClient _dbGraphDbClient;

    public AppServicePlanCrawler(ILogger<AppServicePlanCrawler> logger, IGraphDatabaseClient dbGraphDbClient, ArmClient client)
        : base(logger, dbGraphDbClient, client)
    {
        _logger = logger;
        _dbGraphDbClient = dbGraphDbClient;
    }

    public async override IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        await foreach (var n in base.Crawl(node))
        {
            yield return n;
        }

        var aspNode = (AppServicePlanNode)node;
        _logger.LogDebug($"Crawling App Service Plan {aspNode.ResourceId}");

        var resourceGroupId = ResourceGroupResource.CreateResourceIdentifier(aspNode.SubscriptionId, aspNode.ResourceGroupName);
        var resourceGroup = _armClient.GetResourceGroupResource(resourceGroupId);
        var aspResponse = await resourceGroup.GetAppServicePlanAsync(aspNode.ResourceName);

        if (aspResponse == null || !aspResponse.Value.HasData)
        {
            _logger.LogWarning($"Failed to get App Service Plan {aspNode.ResourceId}.");
            yield break;
        }

        var asp = aspResponse.Value.Data;
        aspNode.NumberOfWorkers = asp.NumberOfWorkers;
        aspNode.Status = asp.Status.ToString();
        aspNode.Kind = asp.Kind.ToString();
        aspNode.MaximumNumberOfWOrkers = asp.MaximumNumberOfWorkers;
        aspNode.GeoRegion = asp.GeoRegion.ToString();
        aspNode.ProvisioningState = asp.ProvisioningState.ToString();
        aspNode.ZoneRedundant = asp.IsZoneRedundant;

        await _dbGraphDbClient.AddOrUpdateNodeAsync(aspNode);
        yield break;
    }
}

