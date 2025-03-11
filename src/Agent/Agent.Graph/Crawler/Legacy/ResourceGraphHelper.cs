using Agent.Data.DatabaseManagers.GraphDatabase;

namespace Agent.Graph.Crawler.Legacy
{
    public class ResourceGraphHelper
    {
        public static async Task<InMemoryGraphManager> ConstructResourceGraphAndPersistAsync(IGraphDatabaseManager graphDatabaseManager)
        {
            var inMemoryGraphManager = await ConstructResourceGraphInMemoryAsync();
            await PersistResourceGraphAsync(
                graphDatabaseManager: graphDatabaseManager,
                resourceGraph: inMemoryGraphManager);

            return inMemoryGraphManager;
        }

        public static async Task<InMemoryGraphManager> ConstructMockResourceGraphAndPersistAsync(IGraphDatabaseManager graphDatabaseManager)
        {
            var inMemoryGraphManager = await ConstructResourceGraphInMemoryAsync();
            await PersistResourceGraphAsync(
                graphDatabaseManager: graphDatabaseManager,
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
            IGraphDatabaseManager graphDatabaseManager,
            InMemoryGraphManager resourceGraph)
        {
            var nodes = resourceGraph.GetAllNodes();
            var edges = resourceGraph.GetAllEdges();

            foreach (var node in nodes)
            {
                await graphDatabaseManager.AddOrUpdateNodeAsync(
                    nodeLabel: "node",
                    nodeId: node.Id,
                    resourceType: node.Type,
                    properties: node.Properties);
            }

            foreach (var edge in edges)
            {
                await graphDatabaseManager.AddOrUpdateEdgeAsync(
                    sourceNodeId: edge.SourceNodeId,
                    targetNodeId: edge.TargetNodeId,
                    relationshipType: edge.RelationshipType);
            }
        }
    }
}
