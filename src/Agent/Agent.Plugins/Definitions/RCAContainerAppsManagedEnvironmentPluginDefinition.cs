using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Framework;
using Agent.Plugins.Kusto;
using Agent.Core.Interfaces;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin]
    public class RCAContainerAppsManagedEnvironmentPluginDefinition
    {
        private readonly IKustoPluginChat _kustoPlugin;
        private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;

        public RCAContainerAppsManagedEnvironmentPluginDefinition(IKustoPluginChat kustoPlugin, IAgentOutboundCommunicationService agentOutboundCommunicationService)
        {
            _kustoPlugin = kustoPlugin;
            _agentOutboundCommunicationService = agentOutboundCommunicationService;
        }

        [Description(
@"Retrieve configuration and provisioning metadata for a specific Azure Container Apps managed environment.

Projects:
- environmentName: Name of the ACA managed environment.
- environmentLocation: Azure region hosting the environment.
- environmentSubscription: Azure subscription ID.
- environmentResourceGroup: Resource group of the environment.
- managedClusterName: Backing AKS cluster name.
- managedClusterLocation: Physical region of the AKS cluster.
- managedSubscription: Subscription of the backing cluster.
- managedClusterCreatedTime: Creation timestamp of the cluster.
- provisioningState: Current provisioning status of the cluster.
- powerState: Power status of the environment.
- chartVersion: Deployed Helm chart version.
- kubernetesVersion: Version of Kubernetes used.
- hasWorkloadProfiles: Indicates if workload profiles are enabled.
- hasCustomerVnet: Indicates if a custom VNet is configured.
- isInternal: Indicates whether the environment is internal-only."
)]
        public async Task<string> GetManagedEnvironmentInfo(
    [Description("Azure region.")] string region,
    [Description("Start time of the query.")] DateTime fromDate,
    [Description("End time of the query.")] DateTime toDate,
    [Description("Name of the managed environment.")] string environmentName,
    [Description("Name of the resource group.")] string resourceGroupName,
    [Description("Azure subscription ID.")] string subscriptionId,
     [Description("provide sampling inputs")] SamplingOptions sampling)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetManagedEnvironment", region,
                new Dictionary<string, string> {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "region", region },
                    { "environmentName", environmentName },
                    { "resourceGroupName", resourceGroupName },
                    { "subscriptionId", subscriptionId }
                }
            );
        }        [KernelFunction("rca_get_changes_in_managed_environment")]
        [Description(
        @"Retrieve configuration state changes for a specific Azure Container Apps managed environment within a given time range.

        This function helps identify if incidents are correlated with configuration changes by highlighting changes that align with the reported issue timeline.

        **Returns a list of component types that are changed**, including their previous and current values during the specified period.
        Note: Unchanged components are NOT returned.
        "
        )]
        public async Task<string> GetChangesInManagedEnvironment(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Customer subscription ID of the managed environment.")] Guid customerSubscriptionId,
            [Description("Name of the customer managed environment.")] string managedEnvironmentName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync(
                "GetChangesinManagedEnvironment",
                region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "customerSubscriptionId", customerSubscriptionId.ToString() },
                    { "managedEnvironmentName", managedEnvironmentName }
                });
        }        [KernelFunction("rca_get_asi_page_for_managed_environment")]
        [Description(
@"Retrieve a direct ASI (App Service Insights) page URL for a given Azure Container Apps managed environment.

Projects:
- region: Azure region hosting the environment.
- environmentName: Name of the ACA managed environment.
- fromDate / toDate: Time window of interest.
- resourceGroupName: Resource group of the environment.
- subscriptionId: Azure subscription ID.
- ASI URL: Clickable diagnostic link for ACA platform health and metadata."
)]
        public Task<string> GetASIPageForManagedEnvironment(
    [Description("Azure region.")] string region,
    [Description("Start time of the query.")] DateTime fromDate,
    [Description("End time of the query.")] DateTime toDate,
    [Description("Name of the managed environment.")] string environmentName,
    [Description("Name of the resource group.")] string resourceGroupName,
    [Description("Azure subscription ID.")] string subscriptionId)
        {
            var basePath = "/services/ACA Azure Container Apps/pages/Container App Environment";

            var cleanPath = Uri.EscapeDataString(basePath); // encodes spaces etc.

            var query = $"environmentLocation={Uri.EscapeDataString(region.ToLowerInvariant())}" +
               $"&environmentName={Uri.EscapeDataString(environmentName)}" +
               $"&environmentResourceGroup={Uri.EscapeDataString(resourceGroupName)}" +
               $"&environmentSubscription={Uri.EscapeDataString(subscriptionId)}" +
               $"&globalFrom={Uri.EscapeDataString(fromDate.ToString("M/d/yyyy hh:mm:ss tt"))}" +
               $"&globalTo={Uri.EscapeDataString(toDate.ToString("M/d/yyyy hh:mm:ss tt"))}";

            var adxUri = $"https://asi.azure.ms{cleanPath}?{query}";

            return Task.FromResult($"ASI Page for managed environment {adxUri}");
        }        [KernelFunction("rca_get_managed_cluster_environment_resource_id")]
        [Description(
        @"Retrieve the Azure Container Apps environment resource identity based on the managed cluster name.
        Projects:
        - managedClusterName: Name of the managed cluster.
        - subscription: Azure subscription ID of the Azure Container Apps environment.
        - resourceGroup: Resource group of the Azure Container Apps environment.
        - environmentName: Name of the Azure Container Apps environment."
        )]
        public async Task<string> GetManagedClusterEnvironmentResourceId(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetManagedClusterEnvironmentResourceId", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName }
                });
        }        [KernelFunction("rca_get_managed_environment_provisioning_status")]
        [Description(
        @"Retrieve the provisioning status of a specific Azure Container Apps managed environment.
        Projects:
        - StartTime: Start time of the reported environment provisioning status.
        - EndTime: End time of the reported environment provisioning status.
        - environmentProvisioningState
        - powerState
        - managedClusterName
        - environmentDeploymentErrors
        - managedClusterProvisioningError."
        )]
        public async Task<string> GetManagedEnvironmentProvisioningStatus(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed environment.")] string environmentName,
            [Description("Name of the resource group.")] string resourceGroupName,
            [Description("Azure subscription ID.")] string subscriptionId)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetManagedEnvironmentProvisioningStatus", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "environmentName", environmentName },
                    { "resourceGroupName", resourceGroupName },
                    { "subscriptionId", subscriptionId }
                });
        }        [KernelFunction("rca_get_managed_environment_admin_events")]
        [Description(
        @"Retrieve the Azure Container Apps environment Admin operation events.
        Projects:
        - PreciseTimeStamp: Timestamp of the event.
        - requestPath: The path of the request.
        - requestMethod: The HTTP method used for the request.
        - statusCode: The status code returned by the request.
        - requestBody: The body of the request.
        - durationInMilliseconds: The duration of the request in milliseconds.
        - env_dt_traceId: The trace ID associated with the event."
        )]
        public async Task<string> GetManagedEnvironmentAdminEvents(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed environment.")] string environmentName,
            [Description("Name of the resource group.")] string resourceGroupName,
            [Description("Azure subscription ID.")] string subscriptionId)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetContainerAppAdminEvents", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "resourceName", environmentName },
                    { "resourceGroupName", resourceGroupName },
                    { "subscriptionId", subscriptionId }
                });
        }        [KernelFunction("rca_get_managed_environment_operation_errors")]
        [Description(
        @"Retrieve the Azure Container Apps environment operation errors.
        Projects:
        - FirstSeen: Timestamp of the first occurrence of the error.
        - LastSeen: Timestamp of the last occurrence of the error.
        - count: The number of times the error has occurred.
        - operationType: The type of operation that caused the error.
        - operationEntityType: The type of entity that the operation was performed on.
        - exception: The exception message associated with the error."
        )]
        public async Task<string> GetManagedEnvironmentOperationErrors(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed environment.")] string environmentName,
            [Description("Name of the resource group.")] string resourceGroupName,
            [Description("Azure subscription ID.")] string subscriptionId)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetManagedEnvironmentOperationErrors", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "environmentName", environmentName },
                    { "resourceGroupName", resourceGroupName },
                    { "subscriptionId", subscriptionId }
                });
        }
    }
}
