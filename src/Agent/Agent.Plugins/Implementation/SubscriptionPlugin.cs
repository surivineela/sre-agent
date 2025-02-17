using Azure.Core;
using Azure;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.AppService;
using Agent.Data.DatabaseManagers.GraphDatabase;
using Agent.Graph;
using Gremlin.Net.Driver;

namespace Agent.Plugins
{
    public class SubscriptionPlugin : ISubscriptionPlugin
    {
        private readonly IGraphDatabaseManager _graphDatabaseManager;

        public SubscriptionPlugin(IGraphDatabaseManager graphDatabaseManager)
        {
            _graphDatabaseManager = graphDatabaseManager;
        }

        public async Task<IReadOnlyList<SubscriptionDescriptor>> ListAllSubscriptionsAsync()
        {
            Console.WriteLine($"[list_azure_subscriptions] Invoked");

            // TODO: This is to limit the output of subscriptions. Update this values as needed. Will need to read it from appsettings.development.json
            string[] displayNameFilter = ["Container Apps Test Resources", "ruslany", "sanmeht", "yanche", "shgup", "pbatum", "mikarmar"];

            var ret = new List<SubscriptionDescriptor>();
            // Authenticate using DefaultAzureCredential
            var credential = new DefaultAzureCredential();
            // Create an instance of the ArmClient to interact with Azure
            var armClient = new ArmClient(credential);
            // Get all subscriptions
            await foreach (var subscription in armClient.GetSubscriptions().GetAllAsync())
            {
                if (displayNameFilter != null && displayNameFilter.Length > 0 &&
                    !displayNameFilter.Any(filter => subscription.Data.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                ret.Add(new SubscriptionDescriptor(
                    Id: subscription.Data.Id,
                    DisplayName: subscription.Data.DisplayName));
            }
            return ret;
        }

        public async Task<IReadOnlyList<AppServiceDescriptor>> ListAppServicesAsync(Guid subscriptionId)
        {
            Console.WriteLine($"[list_app_service_instances] Invoked with subscription {subscriptionId}");

            var appServices = new List<AppServiceDescriptor>();
            string[] rgFilter = ["opagent-poc", "aks-resources", "lgn-rcp-rg-yanchelgn01", "appservices-sre-demo", "pbatum-flex-eus2-demo", "pbatum-sre-demo", "test-apps", "sample-app-rg"];


            try
            {
                // Authenticate using DefaultAzureCredential
                var credential = new DefaultAzureCredential();

                // Create an instance of the ArmClient to interact with Azure
                var armClient = new ArmClient(credential);

                // Construct the Resource Identifier for the specified subscription
                var subscriptionResourceId = new ResourceIdentifier($"/subscriptions/{subscriptionId}");

                // Get the SubscriptionResource
                SubscriptionResource subscription = armClient.GetSubscriptionResource(subscriptionResourceId);

                // Verify if the subscription exists by attempting to get its data
                var subscriptionResponse = await subscription.GetAsync();

                if (subscriptionResponse.Value == null)
                {
                    throw new InvalidOperationException($"Subscription with ID '{subscriptionId}' not found.");
                }

                // Get all resource groups in the subscription
                await foreach (var resourceGroup in subscription.GetResourceGroups().GetAllAsync())
                {
                    if (rgFilter != null && rgFilter.Length > 0 &&
                        !rgFilter.Any(filter => resourceGroup.Data.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    await foreach (var appService in resourceGroup.GetWebSites().GetAllAsync())
                    {
                        var appDescriptor = new AppServiceDescriptor(
                            ResourceId: appService.Id.ToString(),
                            Name: appService.Data.Name,
                            Kind: appService.Data.Kind,
                            Location: appService.Data.Location,
                            Sku: appService.Data.Sku ?? "N/A",
                            State: appService.Data.State,
                            ResourceGroup: appService.Data.ResourceGroup);

                        appServices.Add(appDescriptor);
                    }
                }
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                Console.Error.WriteLine($"Subscription with ID '{subscriptionId}' not found.");
                // Depending on requirements, you might choose to return an empty list or rethrow the exception
                throw;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in ListAppServicesAsync: {ex}");
                throw;
            }

            return appServices;
        }

        public async Task<InMemoryGraphManager> BuildResourceGraphForAllSubscriptionsAsync()
        {
            return await ResourceGraphHelper.ConstructResourceGraphAndPersistAsync(_graphDatabaseManager);
        }

        public async Task<InMemoryGraphManager> BuildMockResourceGraphForAllSubscriptionsAsync()
        {
            return await ResourceGraphHelper.ConstructMockResourceGraphAndPersistAsync(_graphDatabaseManager);
        }

        public async Task DeleteResourceGraph()
        {
            await _graphDatabaseManager.Clear();
        }

        public async Task<ResultSet<dynamic>> QueryResourceGraph(string query)
        {
            return await _graphDatabaseManager.Query(query);
        }
    }
}
