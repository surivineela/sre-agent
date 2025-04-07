// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
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
        _logger.LogDebug($"Crawling for subscription {subNode.SubscriptionId}");

        var subArmId = SubscriptionResource.CreateResourceIdentifier(subNode.SubscriptionId);
        var subResource = _armClient.GetSubscriptionResource(subArmId);

        // the above subResource does not container subscription data, do a Get to get subname
        var subscription = _armClient.GetSubscriptions().Get(subNode.SubscriptionId);
        var subName = subscription?.Value?.Data?.DisplayName;

        var nodeProperties = subNode.GetNodeProperties();
        nodeProperties["subscriptionName"] = subName;
        await _graphDbClient.AddOrUpdateNodeAsync(subNode);

        await foreach (var rg in subResource.GetResourceGroups().GetAllAsync())
        {
            var rgNode = new ResourceGroupNode(subNode.SubscriptionId, rg.Data.Name);
            await _graphDbClient.AddOrUpdateNodeAsync(rgNode);

            var edge = new ArmResourceEdge(subNode.GetNodeId(), rgNode.GetNodeId(), Constants.Relationships.Contains);
            edge.AddOrUpdateEdgeProperty(Constants.RbacPath, Constants.RbacPathInherited);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);

            yield return rgNode;
        }
    }
}
