using Agent.Data.DatabaseManagers.GraphDatabase;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class SubscriptionCrawler : IArmResourceCrawler
{
    private readonly ILogger<SubscriptionCrawler> _logger;
    private readonly IGraphDatabaseManager _dbManager;
    private readonly ArmClient _armClient;

    public SubscriptionCrawler(ILogger<SubscriptionCrawler> logger, IGraphDatabaseManager dbManager, ArmClient armClient)
    {
        _logger = logger;
        _dbManager = dbManager;
        _armClient = armClient;
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

            var edge = new ArmResourceEdge(subNode.GetNodeId(), rgNode.GetNodeId(), Constants.Relationships.Contains);
            edge.AddOrUpdateEdgeProperty(Constants.RbacPath, Constants.RbacPathInherited);
            await _dbManager.AddOrUpdateEdgeAsync(edge.GetSourceNodeId(), edge.GetTargetNodeId(), edge.GetRelationship(), edge.GetEdgeProperties());

            yield return rgNode;
        }
    }
}
