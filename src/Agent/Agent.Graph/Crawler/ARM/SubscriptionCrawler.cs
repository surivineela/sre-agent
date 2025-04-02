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
        await _graphDbClient.AddOrUpdateNodeAsync(subNode.GetNodeLabel(), subNode.GetNodeId(), subNode.GetResourceType(), subNode.GetNodeProperties());

        var subArmId = SubscriptionResource.CreateResourceIdentifier(subNode.SubscriptionId);
        var subResource = _armClient.GetSubscriptionResource(subArmId);

        await foreach (var rg in subResource.GetResourceGroups().GetAllAsync())
        {
            var rgNode = new ResourceGroupNode(subNode.SubscriptionId, rg.Data.Name);
            await _graphDbClient.AddOrUpdateNodeAsync(rgNode.GetNodeLabel(), rgNode.GetNodeId(), rgNode.GetResourceType(), rgNode.GetNodeProperties());

            var edge = new ArmResourceEdge(subNode.GetNodeId(), rgNode.GetNodeId(), Constants.Relationships.Contains);
            edge.AddOrUpdateEdgeProperty(Constants.RbacPath, Constants.RbacPathInherited);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge.GetSourceNodeId(), edge.GetTargetNodeId(), edge.GetRelationship(), edge.GetEdgeProperties());

            yield return rgNode;
        }
    }
}
