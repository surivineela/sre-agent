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
    [AgentToolPlugin(IsFirstPartyOnly = true)]
    public class RCAContainerAppsManagedClusterPluginDefinition
    {
        private readonly IKustoPluginChat _kustoPlugin;

        public RCAContainerAppsManagedClusterPluginDefinition(IKustoPluginChat kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }

        [Description(@"""
        Purpose:
        Retrieves a direct App Service Insights (ASI) page URL for a specific managed cluster associated with an Azure Container Apps environment.

        Scenario:
        Use this tool to get a diagnostic insights link for a managed cluster when the container app name is known.

        Output:
        Returns a string containing the ASI page URL:
        - ASIPageUrl: Direct URL to the ASI diagnostics page for the specified managed cluster
        """
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

        [Description(@"""
        Purpose:
        Retrieves a direct App Service Insights (ASI) page URL for a specific managed cluster associated with an Azure Container Apps environment.

        Scenario:
        Use this tool to get a diagnostic insights link for a managed cluster when the managed cluster name is already known.

        Output:
        Returns a string containing the ASI page URL:
        - ASIPageUrl: Direct URL to the ASI diagnostics page for the specified managed cluster
        """
        )]
        public Task<string> GetASIPageForManagedCluster(
        [Description("Azure region in lower case.")] string region,
        [Description("Start time of the query.")] DateTime fromDate,
        [Description("End time of the query.")] DateTime toDate,
        [Description("Managed cluster name.")] string managedClusterName)
        {
            var basePath = "/services/ACA Azure Container Apps/pages/Managed Cluster";
            #pragma warning disable SYSLIB0013 
            var cleanPath = Uri.EscapeUriString(basePath); // DO NOT CHANGE TO EscapeDataString
            #pragma warning restore SYSLIB0013

            var query = $"managedClusterName={Uri.EscapeDataString(managedClusterName.Trim())}" +
                        $"&globalFrom={Uri.EscapeDataString(fromDate.ToString("M/d/yyyy hh:mm:ss tt"))}" +
                        $"&globalTo={Uri.EscapeDataString(toDate.ToString("M/d/yyyy hh:mm:ss tt"))}";

            var adxUri = $"https://asi.azure.ms{cleanPath}?{query}";

            return Task.FromResult($"ASI Page for managed cluster {adxUri}");
        }

        [Description(@"""
        Purpose:
        Retrieves the ccpNamespace of an ACA's managed cluster, required for other AKS queries.

        Scenario:
        Use this tool to get the ccpNamespace for a managed cluster.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - clusterName: Name of the managed cluster
        - clusterVersion: Version of the cluster
        - resourceGroup: Resource group of the cluster
        - managedResourceGroup: Managed resource group
        - RPTenant: Resource provider tenant
        - clusterBirthdate: Cluster creation date
        - ccpNamespace: CCP namespace of the cluster
        """
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

        [Description(@"""
        Purpose:
        Retrieves system component error events for the given managed cluster.

        Scenario:
        Use this tool to get error events from system components to help diagnose root causes.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - PodName: Name of the pod
        - ContainerName: Name of the container
        - RestartCount: Number of restarts
        - LastStateReason: Reason for the last state
        - LastStateExitCode: Exit code for the last state
        - StateWaitingMessage: Waiting state message
        - StateWaitingReason: Waiting state reason
        """
        )]
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

        [Description(@"""
        Purpose:
        Retrieves system component CPU usage for the given managed cluster.

        Scenario:
        Use this tool to identify system components consuming more than 50% of their allocated CPU limits.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - TimestampUtc: Timestamp of the metric
        - podName: Name of the pod
        - cores: CPU cores used
        - limit: CPU limit for the pod
        - pct: Percentage of CPU used relative to the limit
        """
        )]
        public Task<string> GetSystemComponentCpuUsage(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetSystemComponentCpuUsage", region,
                new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "region", region }
                });
        }
    }
}
