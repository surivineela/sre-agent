using Agent.Graph.Schema;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;

namespace Agent.Graph.Crawler.ARM
{
    public class AppServiceCrawler
    {
        public static async Task<List<Node>> CrawlAllAppServices(InMemoryGraphManager inMemoryGraphManager, List<Node> subscriptionNodes)
        {
            string[] rgFilter = ["opagent-poc", "aks-resources", "lgn-rcp-rg-yanchelgn01", "appservices-sre-demo", "pbatum-flex-eus2-demo", "pbatum-sre-demo", "test-apps", "sample-app-rg", "mikarmar-msha"];

            var subscriptionList = new List<Node>();
            // Authenticate using DefaultAzureCredential
            var credential = new DefaultAzureCredential();
            // Create an instance of the ArmClient to interact with Azure
            var armClient = new ArmClient(credential);

            foreach (var subscriptionNode in subscriptionNodes)
            {
                var subscriptionResourceId = new ResourceIdentifier(subscriptionNode.Id);

                // Get the subscription resource
                var subscriptionResource = await armClient.GetSubscriptionResource(subscriptionResourceId).GetAsync();

                // Get all subscriptions
                await foreach (var resourceGroup in subscriptionResource.Value.GetResourceGroups().GetAllAsync())
                {
                    if (rgFilter != null && rgFilter.Length > 0 &&
                        !rgFilter.Any(filter => resourceGroup.Data.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    await foreach (var appService in resourceGroup.GetWebSites().GetAllAsync())
                    {
                        var appServiceNode = new Node(
                            id: appService.Id,
                            name: appService.Data.Name,
                            type: appService.Data.ResourceType.Type);
                        inMemoryGraphManager.AddOrUpdateNode(appServiceNode);
                        inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                            sourceNode: subscriptionNode,
                            targetNode: appServiceNode,
                            relationshipType: "contains");
                    }

                    await foreach (var appServicePlan in resourceGroup.GetAppServicePlans().GetAllAsync())
                    {
                        var appServicePlanNode = new Node(
                            id: appServicePlan.Id,
                            name: appServicePlan.Data.Name,
                            type: appServicePlan.Data.ResourceType.Type);
                        inMemoryGraphManager.AddOrUpdateNode(appServicePlanNode);
                        inMemoryGraphManager.AddDirectedEdgeIfNotExists(
                            sourceNode: subscriptionNode,
                            targetNode: appServicePlanNode,
                            relationshipType: "contains");
                    }
                }
            }

            return subscriptionList;
        }
    }
}
