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
    }
}
