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
