using Azure.Core;
using Agent.Data.DatabaseManagers.GraphDatabase;
using Azure.Identity;
using Agent.Plugins.Models;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;

namespace Agent.Plugins.Helpers
{
    internal class ResourceGraphHelper
    {
        public static async Task<List<Resource>> FetchResourcesForSubscriptionAsync(string subscriptionIdResourceIdString)
        {
            string[] rgFilter = ["opagent-poc", "aks-resources", "lgn-rcp-rg-yanchelgn01", "appservices-sre-demo", "pbatum-flex-eus2-demo", "pbatum-sre-demo", "test-apps", "sample-app-rg", "mikarmar-msha"];

            var credential = new DefaultAzureCredential();
            var subscriptionResourceId = new ResourceIdentifier(subscriptionIdResourceIdString);
            var armClient = new ArmClient(credential, subscriptionResourceId.SubscriptionId);

            // Get the subscription resource
            var subscription = await armClient.GetSubscriptionResource(subscriptionResourceId).GetAsync();

            // Create the parent resource for the subscription
            var parentResource = new Resource
            {
                Id = subscription.Value.Data.Id,
                Name = subscription.Value.Data.DisplayName,
                Type = "Subscription",
                ChildResources = new List<Resource>()
            };

            await foreach (var resourceGroup in subscription.Value.GetResourceGroups().GetAllAsync())
            {
                if (rgFilter != null && rgFilter.Length > 0 &&
                    !rgFilter.Any(filter => resourceGroup.Data.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                await foreach (var appService in resourceGroup.GetWebSites().GetAllAsync())
                {
                    var childResource = new Resource
                    {
                        Id = appService.Id,
                        Name = appService.Data.Name,
                        Type = appService.Data.ResourceType.Type,
                        ChildResources = new List<Resource>()
                    };

                    parentResource.ChildResources.Add(childResource);
                }
            }

            return new List<Resource> { parentResource };
        }

        public static async Task PersistResourceGraphAsync(
            IGraphDatabaseManager graphDatabaseManager,
            ResourceGraph resourceGraph)
        {
            foreach (var resource in resourceGraph.Resources)
            {
                await PersistSubscriptionResourceGraphAsync(graphDatabaseManager, resource);
            }
        }

        private static async Task PersistSubscriptionResourceGraphAsync(
            IGraphDatabaseManager graphDatabaseManager,
            Resource subscriptionResource)
        {
            if (subscriptionResource == null)
            {
                return;
            }

            await graphDatabaseManager.AddOrUpdateNodeAsync(
                nodeId: subscriptionResource.Id,
                resourceType: subscriptionResource.Type,
                properties: subscriptionResource.GetProperties());

            if (subscriptionResource.ChildResources != null && subscriptionResource.ChildResources.Any())
            {
                foreach (var childResource in subscriptionResource.ChildResources)
                {
                    await graphDatabaseManager.AddOrUpdateNodeAsync(
                        nodeId: childResource.Id,
                        resourceType: childResource.Type,
                        properties: childResource.GetProperties());

                    await graphDatabaseManager.AddEdgeIfNotExistsAsync(
                        sourceNodeId: subscriptionResource.Id,
                        targetNodeId: childResource.Id,
                        relationshipType: "Contains");
                }
            }
        }
    }
}
