// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Data.DatabaseClients.GraphDbClient;
using FirstPartyAgent.Constants;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.Plugins.Definitions
{
    // These are tools exposed to any-sub agent that uses this plugin but mostly it will be used by 'RevisionAgent'
    // Note!!: If this plugin is used by other agent, then we are mixing the concerns and we need to refactor this plugin
    public class ManagedEnvironmentPluginDefinition
    {
        private readonly IManagedEnvironmentPlugin _plugin;

        public ManagedEnvironmentPluginDefinition(IManagedEnvironmentPlugin Plugin)
        {
            _plugin = Plugin;
        }



        [KernelFunction(KernelFunctionNames.ACA.GetManagedEnvironmentInformation)]
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
        public Task<string> GetManagedEnvironmentInfo(
    [Description("Azure region.")] string region,
    [Description("Start time of the query.")] DateTime fromDate,
    [Description("End time of the query.")] DateTime toDate,
    [Description("Name of the managed environment.")] string environmentName,
    [Description("Name of the resource group.")] string resourceGroupName,
    [Description("Azure subscription ID.")] string subscriptionId,
     [Description("provide sampling inputs")] SamplingOptions sampling)
        {
            return _plugin.GetManagedEnvironmentInformation(region.NormalizeLocation(), fromDate, toDate, environmentName, resourceGroupName, subscriptionId);
        }

        [KernelFunction("GetChangesInManagedEnvironment")]
        [Description(
        @"Retrieve configuration state changes for a specific Azure Container Apps managed environment within a given time range.

        This function helps identify if incidents are correlated with configuration changes by highlighting changes that align with the reported issue timeline.

        **Returns a list of component types that are changed**, including their previous and current values during the specified period.
        Note: Unchanged components are NOT returned.
        "
        )]
        public Task<string> GetChangesInManagedEnvironment(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Customer subscription ID of the managed environment.")] Guid customerSubscriptionId,
            [Description("Name of the customer managed environment.")] string managedEnvironmentName)
        {
            return _plugin.GetChangesInManagedEnvironment(region, fromDate, toDate, customerSubscriptionId, managedEnvironmentName);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetASIPageForManagedEnvironment)]
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
            return _plugin.GetASIPageForManagedEnvironment(region, fromDate, toDate, environmentName, resourceGroupName, subscriptionId);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetManagedClusterEnvironmentResourceId)]
        [Description(
        @"Retrieve the Azure Container Apps environment resource identity based on the managed cluster name.
        Projects:
        - managedClusterName: Name of the managed cluster.
        - subscription: Azure subscription ID of the Azure Container Apps environment.
        - resourceGroup: Resource group of the Azure Container Apps environment.
        - environmentName: Name of the Azure Container Apps environment."
        )]
        public Task<string> GetManagedClusterEnvironmentResourceId(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _plugin.GetManagedClusterEnvironmentResourceId(region, fromDate, toDate, managedClusterName);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetManagedEnvironmentProvisioningStatus)]
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
        public Task<string> GetManagedEnvironmentProvisioningStatus(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed environment.")] string environmentName,
            [Description("Name of the resource group.")] string resourceGroupName,
            [Description("Azure subscription ID.")] string subscriptionId)
        {
            return _plugin.GetManagedEnvironmentProvisioningStatus(region, fromDate, toDate, environmentName, resourceGroupName, subscriptionId);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetManagedEnvironmentAdminEvents)]
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
        public Task<string> GetManagedEnvironmentAdminEvents(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed environment.")] string environmentName,
            [Description("Name of the resource group.")] string resourceGroupName,
            [Description("Azure subscription ID.")] string subscriptionId)
        {
            return _plugin.GetManagedEnvironmentAdminEvents(region, fromDate, toDate, environmentName, resourceGroupName, subscriptionId);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetManagedEnvironmentOperationErrors)]
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
        public Task<string> GetManagedEnvironmentOperationErrors(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed environment.")] string environmentName,
            [Description("Name of the resource group.")] string resourceGroupName,
            [Description("Azure subscription ID.")] string subscriptionId)
        {
            return _plugin.GetManagedEnvironmentOperationErrors(region, fromDate, toDate, environmentName, resourceGroupName, subscriptionId);
        }
    }
}
