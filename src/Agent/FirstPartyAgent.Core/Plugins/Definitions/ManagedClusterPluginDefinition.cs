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
    public class ManagedClusterPluginDefinition
    {
        private readonly IManagedClusterPlugin _plugin;

        public ManagedClusterPluginDefinition(IManagedClusterPlugin Plugin)
        {
            _plugin = Plugin;
        }

        [KernelFunction(KernelFunctionNames.ACA.GetManagedClusterInformation)]
        [Description(
@"Retrieve managed cluster configuration and provisioning metadata for a given environment.
Projects:
- managedClusterName: Name of the Kubernetes cluster.
- managedClusterLocation: Physical Azure region.
- managedSubscription: Subscription ID tied to the cluster.
- managedClusterCreatedTime: Timestamp when the cluster was created.
- powerState: Current power status of the cluster.
- provisioningState: Provisioning status (Succeeded, Failed, etc).
- chartVersion: Helm chart version deployed.
- chartVersionUpgradeTime: When the chart was last upgraded.
- kubernetesVersion: Current Kubernetes version.
- environmentName: Associated ACA environment.
- environmentLocation: ACA environment location.
- hasWorkloadProfiles: Whether workload profiles are enabled.
- customVnet: Whether a customer VNet is configured.
- RegionalConsumptionV2: Indicates usage tier for V2 environments."
)]
        public Task<string> GetManagedClusterInformation(
        [Description("Azure region.")] string region,
      [Description("Start time of the query.")] DateTime fromDate,
      [Description("End time of the query.")] DateTime toDate,
      [Description("Name of the container app.")] string containerAppName,
      [Description("Name of the resource group.")] string resourceGroupName,
      [Description("Azure subscription ID.")] string subscriptionId,
             [Description("provide sampling inputs")] SamplingOptions sampling)
        {
            return _plugin.GetManagedClusterInformation(region, fromDate, toDate, containerAppName, resourceGroupName, subscriptionId, sampling);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetASIPageForManagedCluster)]
        [Description(
 @"Retrieve a direct ASI (App Service Insights) page URL for a specific **Managed Cluster** associated with an Azure Container Apps environment.
This link provides diagnostic insights into the cluster hosting the ACA environment.

Inputs:
- region: Azure region where the cluster is deployed.
- containerAppName: Name of any container app associated with the managed environment.
- fromDate / toDate: Time range for diagnostic analysis.
- resourceGroupName: Resource group of the ACA environment.
- subscriptionId: Azure subscription ID."
 )]
        public Task<string> GetASIPageForManagedCluster(
     [Description("Azure region.")] string region,
     [Description("Start time of the query.")] DateTime fromDate,
     [Description("End time of the query.")] DateTime toDate,
     [Description("Name of the container app. Used to resolve the environment context.")] string containerAppName,
     [Description("Name of the resource group hosting the ACA environment.")] string resourceGroupName,
     [Description("Azure subscription ID.")] string subscriptionId)
        {
            return _plugin.GetASIPageForManagedCLuster(region, fromDate, toDate, containerAppName, resourceGroupName, subscriptionId);
        }

    }
}
