// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.Legacy;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins
{
    public class SubscriptionPlugin : ISubscriptionPlugin
    {
        private readonly IGraphDatabaseClient _graphDbClient;
        private readonly ILogger<SubscriptionPlugin> _logger;

        public SubscriptionPlugin(IGraphDatabaseClient graphDatabaseManager, ILogger<SubscriptionPlugin> logger)
        {
            _graphDbClient = graphDatabaseManager;
            _logger = logger;
        }

        public async Task<IReadOnlyList<SubscriptionDescriptor>> ListAllSubscriptionsAsync()
        {
            try
            {
                _logger.LogInformation($"[list_azure_subscriptions] Invoked");

                var ret = new List<SubscriptionDescriptor>();
                // Authenticate using DefaultAzureCredential
                var credential = new DefaultAzureCredential();
                // Create an instance of the ArmClient to interact with Azure
                var armClient = new ArmClient(credential);
                // Get all subscriptions
                await foreach (var subscription in armClient.GetSubscriptions().GetAllAsync())
                {
                    ret.Add(new SubscriptionDescriptor(
                        Id: subscription.Data.Id,
                        DisplayName: subscription.Data.DisplayName));
                }
                return ret;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ListAllSubscriptionsAsync");
                return [];
            }
        }

        public async Task<IReadOnlyList<AppServiceDescriptor>> ListAppServicesAsync(Guid subscriptionId)
        {
            _logger.LogInformation($"[list_app_service_instances] Invoked with subscription {subscriptionId}");

            var appServices = new List<AppServiceDescriptor>();

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
                _logger.LogInformation($"Subscription with ID '{subscriptionId}' not found.");
                return [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in ListAppServicesAsync with subscription {subscriptionId}");
                return [];
            }

            return appServices;
        }

        public async Task<InMemoryGraphManager> BuildResourceGraphForAllSubscriptionsAsync()
        {
            return await ResourceGraphHelper.ConstructResourceGraphAndPersistAsync(_graphDbClient);
        }

        public async Task<InMemoryGraphManager> BuildMockResourceGraphForAllSubscriptionsAsync()
        {
            return await ResourceGraphHelper.ConstructMockResourceGraphAndPersistAsync(_graphDbClient);
        }
    }
}
