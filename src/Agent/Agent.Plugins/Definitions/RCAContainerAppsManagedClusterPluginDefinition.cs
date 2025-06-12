using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Interfaces;
using Agent.Framework;
using Agent.Plugins.Interface;
using Agent.Plugins.KustoPlugin;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin]
    public class RCAContainerAppsManagedClusterPluginDefinition
    {
        private readonly IKustoPluginChat _kustoPlugin;

        public RCAContainerAppsManagedClusterPluginDefinition(IKustoPluginChat kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }

        [Description(
         @"Retrieve a direct ASI (App Service Insights) page URL for a specific **Managed Cluster** associated with an Azure Container Apps environment.
        This link provides diagnostic insights into the cluster hosting the ACA environment.
        **Note: Use this when specific container app name is known**
        "
         )]
        public async Task<string> GetASIPageForManagedClusterForApp(
         [Description("Azure region.")] string region,
         [Description("Start time of the query.")] DateTime fromDate,
         [Description("End time of the query.")] DateTime toDate,
         [Description("Name of the container app. Used to resolve the environment context.")] string containerAppName,
         [Description("Name of the resource group hosting the ACA environment.")] string resourceGroupName,
         [Description("Azure subscription ID.")] string subscriptionId)
        {
            var clusterName = await _kustoPlugin.ExecuteFunctionAsync("GetManagedClusterName", region,
                new Dictionary<string, string> {
            { "containerAppNameParam", containerAppName },
            { "resourceGroupParam", resourceGroupName },
            { "subscriptionParam", subscriptionId }
                }); 
            return await GetASIPageForManagedCluster(region, fromDate, toDate, clusterName.Result);
        }

        [Description(
        @"Retrieve a direct ASI (App Service Insights) page URL for a specific **Managed Cluster** associated with an Azure Container Apps environment.
        This link provides diagnostic insights into the cluster hosting the ACA environment.
        **Note: Use this when managed cluster name  like 'calmisland-41ad83b9' is already known**
        "
        )]
        public Task<string> GetASIPageForManagedCluster(
        [Description("Azure region in lower case")] string region,
        [Description("Start time of the query.")] DateTime fromDate,
        [Description("End time of the query.")] DateTime toDate,
        [Description("Managed cluster name")] string managedClusterName)
        {
            var basePath = "/services/ACA Azure Container Apps/pages/Managed Cluster";
            var cleanPath = Uri.EscapeUriString(basePath); // DO NOT CHANGE TO EscapeDataString

            var query = $"managedClusterName={Uri.EscapeDataString(managedClusterName.Trim())}" +
                        $"&globalFrom={Uri.EscapeDataString(fromDate.ToString("M/d/yyyy hh:mm:ss tt"))}" +
                        $"&globalTo={Uri.EscapeDataString(toDate.ToString("M/d/yyyy hh:mm:ss tt"))}";

            var adxUri = $"https://asi.azure.ms{cleanPath}?{query}";

            return Task.FromResult($"ASI Page for managed cluster {adxUri}");
        }

        [Description(
        @"Retrieve the ccpNamespace of ACA's cluster, which is a needed parameter for other aks query 

        Inputs:
        - region: Azure region where the cluster is deployed.
        - fromDate / toDate: Time range for diagnostic analysis.
        - resourceGroupName: Resource group of the ACA environment.
        - subscriptionId: Azure subscription ID.
        - managedClusterName: Name of the managed cluster."
        )]
        public Task<string> GetAksClusterCcpNamespace(
        [Description("Azure region.")] string region,
        [Description("Start time of the query.")] DateTime fromDate,
        [Description("End time of the query.")] DateTime toDate,
        [Description("Name of the resource group hosting the ACA environment.")] string resourceGroupName,
        [Description("Azure subscription ID.")] string subscriptionId,
        [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("GetAksClusterCcpNamespace", "akshuba.centralus", "AKSprod",
                new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "resourceGroupName", resourceGroupName },
            { "subscriptionId", subscriptionId },
            { "managedClusterName", managedClusterName },
                });
        }

        // ToDo: Add more possible errors like PodNotschedulable, NodeNotReady, etc errors.
        [Description(@"
@Retrieve system component error events for the given managed cluster. The system component error events might provide diagnostic
information to investigate the root cause of the issue.

Inputs:
- region: Azure region where the cluster is deployed.
- fromDate / toDate: Time range for diagnostic analysis.
- managedClusterName: Name of the managed cluster.
")]
        public Task<string> GetSystemComponentErrorEvents(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetSystemComponentErrorEvents", region,
                new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName }
                });
        }
    }
}
