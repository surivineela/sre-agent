// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Schema;
using Gremlin.Net.Driver;

namespace Agent.Plugins.Mocks
{
    public class MockGraphDBPlugin : IGraphDBPlugin
    {
        private List<string> _containerAppsWithNodesWithoutSourceCodeNodes;
        private Dictionary<string, string> _containerAppsToSourceCodeNodeMapping;
        private List<string> _reposScanned;
        private Dictionary<string, string> _aksDependencyDescriptions;

        public MockGraphDBPlugin()
        {
            _containerAppsToSourceCodeNodeMapping = new Dictionary<string, string>();
            _reposScanned = new List<string>();
            _aksDependencyDescriptions = new Dictionary<string, string>();
        }

        public MockGraphDBPlugin(List<string> containerAppsWithNodesWithoutSourceCodeNodes)
            : this()
        {
            _containerAppsWithNodesWithoutSourceCodeNodes = containerAppsWithNodesWithoutSourceCodeNodes;
        }

        public Guid? ThreadId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public Dictionary<string, string> GetContainerAppsToSourceCodeNodeMapping()
        {
            return _containerAppsToSourceCodeNodeMapping;
        }

        public async Task<dynamic> GetManagedResourcesInfoAsync()
        {
            // This is a mock implementation that returns dummy data
            try
            {
                // Create mock data for Azure resources
                var managedResources = new Dictionary<string, long>
                {
                    { "containerapps", 3 },
                    { "servers", 2 },
                    { "databaseaccounts", 1 },
                    { "redis", 1 },
                    { "sites", 5 },
                    { "managedclusters", 2 },
                    { "virtualnetworks", 3 },
                    { "storageaccounts", 4 },
                    { "namespaces", 2 }
                };

                // Create mock data for other resources
                var otherResources = new Dictionary<string, long>
                {
                    { "repository", 7 }
                };

                var totalAzureCount = managedResources.Values.Sum();
                var totalOtherCount = otherResources.Values.Sum();

                return new
                {
                    AzureResources = managedResources,
                    OtherResources = otherResources,
                    TotalAzureResourceCount = totalAzureCount,
                    TotalOtherResourceCount = totalOtherCount,
                    TotalResourceCount = totalAzureCount + totalOtherCount
                };
            }
            catch (Exception ex)
            {
                // Log error and rethrow
                Console.WriteLine($"Error getting managed resources information: {ex.Message}");
                throw;
            }
        }

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
            _containerAppsToSourceCodeNodeMapping[resourceId] = repoUrl;
            return Task.CompletedTask;
        }

        public Task<string> FindAllNetworkConnectedResources(string resourceId = "")
        {
            throw new NotImplementedException();
        }

        public Task<List<string>> GetContainerAppsWithNodesWithoutSourceCodeNodesAsync()
        {
            return Task.FromResult(_containerAppsWithNodesWithoutSourceCodeNodes ?? new List<string>());
        }

        public Task<ResultSet<dynamic>> Query(string query)
        {
            throw new NotImplementedException();
        }

        public Task<string> VisualizeApplicationComponents(string resourceId, int hops = 3, Guid? threadId = null)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetGeneralHealthAsync(string resourceName, string resourceType)
        {
            throw new NotImplementedException();
        }

        public Task<List<ArmResourceNode>> SearchResourceAsync(string resourceName, string resourceType)
        {
            throw new NotImplementedException();
        }

        public Task<List<ArmResourceNode>> SearchResourceByNameAsync(string resourceName)
        {
            throw new NotImplementedException();
        }

        public Task<List<dynamic>> ListSubscriptionsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<string> FetchAndSummarizeActivityLogs(string resourceId, int daysBack = 30, Guid? threadId = null)
        {
            throw new NotImplementedException();
        }

        public Task<dynamic> GetResourceCountAsync(string resourceType, string groupBy = "")
        {
            throw new NotImplementedException();
        }

        public Task<List<Dictionary<string, object>>> ListResourcesByTypeAsync(string resourceType, string propertyName, string propertyValue)
        {
            throw new NotImplementedException();
        }

        public string GetKnowledgeGraphResourceUsageDashboard()
        {
            throw new NotImplementedException();
        }

        public void ConfigureAKSMicroservices(string AKSClusterResourceId, string _namespace, string deploymentName, string dependency)
        {
            string key = $"{AKSClusterResourceId}:{_namespace}:{deploymentName}";
            _aksDependencyDescriptions[key] = dependency;
        }

        public Task<string> VisualizeAKSMicroserviceTopology(string AKSClusterResourceId, string _namespace, string deploymentName, Guid? threadId = null)
        {
            string key = $"{AKSClusterResourceId}:{_namespace}:{deploymentName}";
            if (_aksDependencyDescriptions.TryGetValue(key, out string dependency))
            {
                return Task.FromResult(dependency);
            }

            return Task.FromResult("No dependency information available");
        }

        public Task<Dictionary<string, object>> GetResourceBasicProperties(string resourceId)
        {
            throw new NotImplementedException();
        }

        public Task<Dictionary<string, object>> GetResourceDetailedProperties(string resourceId)
        {
            throw new NotImplementedException();
        }

        public Task UpdateRepoNodeWithLastScanTime(string repoUrl)
        {
            _reposScanned.Add(repoUrl);
            return Task.CompletedTask;
        }

        public List<string> GetReposScanned()
        {
            return _reposScanned;
        }
    }
}

