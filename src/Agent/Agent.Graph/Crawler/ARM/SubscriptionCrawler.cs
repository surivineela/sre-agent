// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Interfaces;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class SubscriptionCrawler : IResourceCrawler
{
    private readonly ILogger<SubscriptionCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly ArmClient _armClient;

    public SubscriptionCrawler(ILogger<SubscriptionCrawler> logger, IGraphDatabaseClient graphDbClient, ArmClient armClient)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
        _armClient = armClient;
    }

    public async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        var subNode = (SubscriptionNode)node;
        _logger.LogInternalInformation($"Crawling for subscription {subNode.SubscriptionId}");

        var subArmId = SubscriptionResource.CreateResourceIdentifier(subNode.SubscriptionId);
        var subResource = _armClient.GetSubscriptionResource(subArmId);

        // the above subResource does not container subscription data, do a Get to get subname
        var subscription = _armClient.GetSubscriptions().Get(subNode.SubscriptionId);
        var subName = subscription?.Value?.Data?.DisplayName;

        if (string.IsNullOrEmpty(subName))
        {
            _logger.LogInternalWarning($"Subscription name is null or empty for subscription ID {subNode.SubscriptionId}");
            yield break; // Exit if we cannot get the subscription name
        }
       
        var nodeProperties = subNode.GetNodeProperties();
        nodeProperties["subscriptionName"] = subName;
        await _graphDbClient.AddOrUpdateNodeAsync(subNode.GetNodeLabel(), subNode.GetNodeId(), subNode.GetResourceType(), nodeProperties);

        await foreach (var rg in subResource.GetResourceGroups().GetAllAsync())
        {
            var rgNode = new ResourceGroupNode(subNode.SubscriptionId, rg.Data.Name);
            await _graphDbClient.AddOrUpdateNodeAsync(rgNode);

            var edge = new ArmResourceEdge(subNode.GetNodeId(), rgNode.GetNodeId(), Constants.Relationships.Contains);
            edge.AddOrUpdateEdgeProperty(Constants.RbacPath, Constants.RbacPathInherited);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);

            yield return rgNode;
        }

        var deleteBefore = DateTimeOffset.UtcNow;

        var props = new Dictionary<string, string>
        {
            { "subscriptionId", subNode.SubscriptionId },
        };
        await CrawlerExtensions.SoftDeleteStaleNodesWithFilter(_graphDbClient, props, deleteBefore);
    }
}
