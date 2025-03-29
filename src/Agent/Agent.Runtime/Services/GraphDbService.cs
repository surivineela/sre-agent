using Agent.Data.DatabaseClients.GraphDbClient;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services
{
    public class GraphDbService(ILogger<GraphDbService> logger, IGraphDatabaseClient gremlinClient) : IGraphDbService
    {
        public async Task<List<ArmResourceNode>> GetAllResourceNodes()
        {
            logger.LogInformation("Fetching all resource nodes from the graph database.");
            var allResourceNodes = await gremlinClient.Query("g.V().project('resourceType', 'resourceName','resourceGroupName','subscriptionId', 'resourceId').by(coalesce(values('resourceType'), constant('MISSING'))).by(coalesce(values('resourceName'), constant('MISSING'))).by(coalesce(values('resourceGroupName'), constant('MISSING'))).by(coalesce(values('subscriptionId'), constant('MISSING'))).by(coalesce(values('resourceId'), constant('MISSING')))");

            if (allResourceNodes is null || allResourceNodes.Count == 0)
            {
                logger.LogWarning("No resource nodes found in the graph database.");
                return [];
            }

            logger.LogInformation($"Fetched {allResourceNodes.Count} resource nodes from the graph database.");
            
            return [.. allResourceNodes.Select(node => new ArmResourceNode
            {
                ResourceType = node["resourceType"],
                ResourceName = node["resourceName"],
                ResourceGroupName = node["resourceGroupName"],
                SubscriptionId = node["subscriptionId"],
                ResourceId = node["resourceId"]
            })];
        }
    }
}
