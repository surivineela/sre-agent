using Agent.Data.DatabaseClients.GraphDbClient;

namespace Agent.Graph.Crawler.Legacy
{
    public class ResourceGraphHelper
    {
        public static async Task<InMemoryGraphManager> ConstructResourceGraphAndPersistAsync(IGraphDatabaseClient graphDbClient)
        {
            var inMemoryGraphManager = await ConstructResourceGraphInMemoryAsync();
            await PersistResourceGraphAsync(
                graphDbClient: graphDbClient,
                resourceGraph: inMemoryGraphManager);

            return inMemoryGraphManager;
        }

        public static async Task<InMemoryGraphManager> ConstructMockResourceGraphAndPersistAsync(IGraphDatabaseClient graphDbClient)
        {
            var inMemoryGraphManager = await ConstructResourceGraphInMemoryAsync();
            await PersistResourceGraphAsync(
                graphDbClient: graphDbClient,
                resourceGraph: inMemoryGraphManager);

            return inMemoryGraphManager;
        }

        public static async Task<InMemoryGraphManager> ConstructResourceGraphInMemoryAsync()
        {
            var inMemoryGraphManager = new InMemoryGraphManager();
            var subscriptionNodes = await SubscriptionCrawler.CrawlAllSubscriptions(inMemoryGraphManager);
            await AppServiceCrawler.CrawlAllAppServices(inMemoryGraphManager, subscriptionNodes);
            return inMemoryGraphManager;
        }

        public static async Task<InMemoryGraphManager> ConstructMockResourceGraphInMemoryAsync()
        {
            var inMemoryGraphManager = new InMemoryGraphManager();
            await MockCrawler.CrawlMock(inMemoryGraphManager);
            return inMemoryGraphManager;
        }

        public static async Task PersistResourceGraphAsync(
            IGraphDatabaseClient graphDbClient,
            InMemoryGraphManager resourceGraph)
        {
            var nodes = resourceGraph.GetAllNodes();
            var edges = resourceGraph.GetAllEdges();

            foreach (var node in nodes)
            {
                await graphDbClient.AddOrUpdateNodeAsync(
                    nodeLabel: "node",
                    nodeId: node.Id,
                    resourceType: node.Type,
                    properties: node.Properties);
            }

            foreach (var edge in edges)
            {
                await graphDbClient.AddOrUpdateEdgeAsync(
                    sourceNodeId: edge.SourceNodeId,
                    targetNodeId: edge.TargetNodeId,
                    relationshipType: edge.RelationshipType);
            }
        }
    }
}
