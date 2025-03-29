using Agent.Graph.Schema;
using Gremlin.Net.Driver;

namespace Agent.Plugins.Mocks
{
    public class MockGraphDBPlugin : IGraphDBPlugin
    {
        public string? ThreadId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public Task<List<ApplicationGraph>> DiscoverApplications(string subscriptionId)
        {
            throw new NotImplementedException();
        }

        public Task<List<Node>> GetApplicationComponentsSummary(string resourceId, int hops = 3)
        {
            throw new NotImplementedException();
        }

        public Task AddSourceCodeNodeToContainerAppNodeAsync(string resourceId, string repoUrl)
        {
            throw new NotImplementedException();
        }

        public Task<string> FindAllNetworkConnectedResources(string resourceId = "")
        {
            throw new NotImplementedException();
        }

        public Task<List<string>> GetContainerAppsWithNodesWithoutSourceCodeNodesAsync()
        {
            throw new NotImplementedException();
        }

        public Task<ResultSet<dynamic>> Query(string query)
        {
            throw new NotImplementedException();
        }

        public Task<string> VisualizeApplicationComponents(string resourceId, int hops = 3, Guid? threadId = null)
        {
            throw new NotImplementedException();
        }
    }
}
