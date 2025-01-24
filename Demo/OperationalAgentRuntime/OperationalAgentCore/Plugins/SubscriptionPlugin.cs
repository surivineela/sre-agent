using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Support;
using Azure.ResourceManager.Resources;
using HarfBuzzSharp;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Azure.ResourceManager.Support.Models;
using Microsoft.IdentityModel.Tokens;

namespace OperationalAgentRuntime.Cli
{
    public class SubscriptionPlugin
    {
        /// <summary>
        /// Descriptor for App Service instances.
        /// </summary>
        public sealed record AppServiceDescriptor(
            string ResourceId,
            string Name,
            [Description("app means WebApp, functionapp means FunctionApp")]
            string Kind,
            string Location,
            string Sku,
            string State,
            string ResourceGroup);

        [Description("The id and display name of an Azure subscription")]
        public sealed record SubscriptionDescriptor(
            string Id,
            string DisplayName);

        public sealed record SkuDescriptor(
            string Name,
            string Tier,
            string Size,
            string Family,
            int Capacity);

        public sealed record SupportTicketDescriptor(
            string TicketId,
            string Title,
            string Status,
            string Severity,
            string CreatedAt,
            string UpdatedAt);

        /// <summary>
        /// Lists all App Services within a specified subscription.
        /// </summary>
        /// <param name="subscriptionId">The Azure subscription ID.</param>
        /// <returns>A list of AppServiceDescriptor objects.</returns>
        [KernelFunction("list_azure_subscriptions")]
        [Description("Gets a list of Azure subscriptions that a user has access to")]
        public async Task<IReadOnlyList<SubscriptionDescriptor>> ListSubscriptions()
        {
            Console.WriteLine($"[list_azure_subscriptions] Invoked");

            // TODO: This is to limit the output of subscriptions. Update this values as needed. Will need to read it from appsettings.development.json
            string[] displayNameFilter = ["Container Apps Test Resources", "ruslany", "sanmeht", "yanche"];

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

        [KernelFunction("open_support_ticket")]
        [Description("Opens a support ticket that describes a problem with an app service")]
        public async Task<SupportTicketDescriptor> OpenSupportTicket(string resourceId, string description)
        {
            Console.WriteLine($"[open_support_ticket] Invoked");

            // Authenticate using DefaultAzureCredential
            var credential = new DefaultAzureCredential();
            // Create an instance of the ArmClient to interact with Azure
            var armClient = new ArmClient(credential);

            // Parse the ARM resourceId
            var armResourceId = new ResourceIdentifier(resourceId);

            // Construct the Resource Identifier for the specified subscription
            var subscriptionResourceId = new ResourceIdentifier($"/subscriptions/{armResourceId.SubscriptionId}");
            
            try
            {
                // Get the SubscriptionResource
                SubscriptionResource subscription = armClient.GetSubscriptionResource(subscriptionResourceId);
                var supportTicketsCollection = subscription.GetSubscriptionSupportTickets();

                var supportTicketName = Guid.NewGuid().ToString();

                var supportTicketData = new SupportTicketData(
                    description: "IGNORE: This is a test support ticket opened by Azure Operation Agent",
                    problemClassificationId: "/providers/Microsoft.Support/services/b452a42b-3779-64de-532c-8a32738357a6/problemClassifications/4d30ceba-cf43-f582-9906-ea17b33df58d",
                    severity: SupportSeverityLevel.Minimal,
                    advancedDiagnosticConsent: AdvancedDiagnosticConsent.No,
                    contactDetails: new SupportContactProfile(
                        firstName: "Ruslan",
                        lastName: "Yakushev",
                        preferredContactMethod: PreferredContactMethod.Email,
                        primaryEmailAddress: "ruslany@microsoft.com",
                        preferredTimeZone: "UTC",
                        preferredSupportLanguage: "en-us",
                        country: "USA"),
                    title: "IGNORE: Test Support Ticket",
                    serviceId: "/providers/Microsoft.Support/services/b452a42b-3779-64de-532c-8a32738357a6"
                    );

                var response = await supportTicketsCollection.CreateOrUpdateAsync(WaitUntil.Completed, supportTicketName, supportTicketData);

                var createdSupportTicket = (await supportTicketsCollection.GetAsync(supportTicketName)).Value;

                var result = new SupportTicketDescriptor(
                    TicketId: createdSupportTicket.Data.Name,
                    Title: createdSupportTicket.Data.Title,
                    Status: createdSupportTicket.Data.Status,
                    Severity: createdSupportTicket.Data.Severity.ToString(),
                    CreatedAt: DateTime.UtcNow.ToString("O"),
                    UpdatedAt: DateTime.UtcNow.ToString("O"));

                return result;
            }
            catch (RequestFailedException ex)
            {
                Console.Error.WriteLine($"Error in OpenSupportTicket: {ex.Message}");
                throw;
            }
        }

        [KernelFunction("list_app_service_instances")]
        [Description("Gets a list of Microsoft.Web instances in a specific subscription, including WebApps and FunctionApps with detailed information.")]
        public async Task<IReadOnlyList<AppServiceDescriptor>> ListAppServicesAsync(Guid subscriptionId)
        {
            Console.WriteLine($"[list_app_service_instances] Invoked with subscription {subscriptionId}");

            var appServices = new List<AppServiceDescriptor>();

            string[] rgFilter = ["opagent-poc", "aks-resources"];

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
                    // filter out resource groups that do not match the rgFilter
                    if (rgFilter != null && rgFilter.Length > 0 &&
                        !rgFilter.Any(filter => resourceGroup.Data.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    // Get App Services within the resource group
                    await foreach (var appService in resourceGroup.GetWebSites().GetAllAsync())
                    {
                        appServices.Add(new AppServiceDescriptor(
                            ResourceId: appService.Id.ToString(),
                            Name: appService.Data.Name,
                            Kind: appService.Data.Kind,
                            Location: appService.Data.Location,
                            Sku: appService.Data.Sku ?? "N/A",
                            State: appService.Data.State,
                            ResourceGroup: appService.Data.ResourceGroup));
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
                // Implement proper logging here
                Console.Error.WriteLine($"Error in ListAppServicesAsync: {ex.Message}");
                // Depending on requirements, rethrow or handle the exception accordingly
                throw;
            }

            return appServices;
        }
        
        /// <summary>
        /// Checks if a specific App Service exists within a given subscription and resource group.
        /// </summary>
        /// <param name="subscriptionId">The Azure subscription ID.</param>
        /// <param name="resourceGroup">The resource group name.</param>
        /// <param name="appName">The App Service name.</param>
        /// <returns>True if the App Service exists; otherwise, false.</returns>
        [KernelFunction("check_if_app_exists")]
        [Description("Validates if a webapp/app service/functionapp exists. Requires subscription ID, resource group, and app name.")]
        public async Task<bool> CheckIfAppExistsAsync(Guid subscriptionId, string resourceGroup, string appName)
        {
            try
            {
                Console.WriteLine($"[check_if_app_exists] Invoked with subscription {subscriptionId}, resourceGroup: {resourceGroup}, appName: {appName}");

                // Validate input parameters
                if (subscriptionId == Guid.Empty)
                {
                    throw new ArgumentException("Invalid subscription ID provided.", nameof(subscriptionId));
                }

                if (string.IsNullOrWhiteSpace(resourceGroup))
                {
                    throw new ArgumentException("Resource group name cannot be null or empty.", nameof(resourceGroup));
                }

                if (string.IsNullOrWhiteSpace(appName))
                {
                    throw new ArgumentException("App name cannot be null or empty.", nameof(appName));
                }

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

                // Get the specified resource group
                ResourceGroupResource rg = await subscription.GetResourceGroups().GetAsync(resourceGroup);

                if (rg == null)
                {
                    Console.Error.WriteLine($"Resource group '{resourceGroup}' not found in subscription '{subscriptionId}'.");
                    return false;
                }

                // Attempt to get the App Service
                var appServiceResponse = await rg.GetWebSites().GetAsync(appName);

                return appServiceResponse.Value != null;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // App Service not found
                return false;
            }
            catch (Exception ex)
            {
                // Implement proper logging here
                Console.Error.WriteLine($"Error in CheckIfAppExistsAsync: {ex.Message}");
                // Depending on requirements, rethrow or handle the exception accordingly
                throw;
            }
        }
    }
}
