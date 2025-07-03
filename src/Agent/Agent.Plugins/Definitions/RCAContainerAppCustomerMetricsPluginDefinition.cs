// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Framework;
using Agent.Plugins.Interface;
using Agent.Plugins.KustoPlugin;
using Agent.Plugins.Services.Interfaces;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(IsFirstPartyOnly = true)]
    public class RCAContainerAppCustomerMetricsPluginDefinition
    {
        private readonly IKustoPluginChat _kustoPlugin;
        public RCAContainerAppCustomerMetricsPluginDefinition(IKustoPluginChat kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }

        [Description(@"""
Retrieves the underlying infrastructure layer (AKS or Legion) for a container app.
Use this tool to determine the infrastructure type for a specific container app.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- InfraLayer: The infrastructure layer, either 'AKS' or 'Legion'.
"""
)]
        public Task<string> GetContainerAppInfraLayer(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Azure subscription ID.")] string subscriptionId,
            [Description("Resource group name.")] string resourceGroupName,
            [Description("Container app name.")] string containerAppName,
            [Description("Managed cluster name.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetContainerAppInfraLayer", region,
                new Dictionary<string, string>
                {
                            { "fromDate", fromDate.ToString() },
                            { "toDate", toDate.ToString() },
                            { "region", region },
                            { "subscriptionId", subscriptionId },
                            { "resourceGroupName", resourceGroupName },
                            { "containerAppName", containerAppName },
                            { "managedClusterName", managedClusterName }
                });
        }

        [Description(@"""
Checks if metrics were missed for a container app in the specified time range.
Use this tool to identify if any metrics are missing for a given metric name and container app.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- MetricsMissed: 'True' if metrics were missed, otherwise 'False'.
"""
)]
        public Task<string> GetMetricsMdmCount(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Metric name to check.")] string metricName,
            [Description("ARM ID of the container app.")] string containerAppArmId
        )
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetMetricsMdmCount", region,
             new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "region", region },
                        { "metricName", metricName },
                        { "containerAppArmId", containerAppArmId }
             });
        }

        [Description(@"""
Retrieves missed times for MDM pod heartbeats in a managed cluster within a time range.
Use this tool to find times when MDM pod heartbeats were missed.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- PreciseTimeStamp: Timestamp when a heartbeat was missed.
"""
)]
        public Task<string> GetMdmPodHeartbeatMissedTimes(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Managed cluster name.")] string managedClusterName
        )
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetMdmPodHeartbeatMissedTimes", region,
                new Dictionary<string, string>
                {
                            { "fromDate", fromDate.ToString() },
                            { "toDate", toDate.ToString() },
                            { "managedClusterName", managedClusterName}
                });
        }

        [Description(@"""
Retrieves times when a specific metric was missed for a container app in the specified time range.
Use this tool to get timestamps where the specified metric was not reported.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- TimestampUtc: Timestamp when the metric was missed.
"""
)]
        public Task<string> GetMissedMdmMetricTimes(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Metric name to check.")] string metricName,
            [Description("ARM ID of the container app.")] string containerAppArmId
        )
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetMissedMdmMetricTimes", region,
                new Dictionary<string, string>
                {
                            { "fromDate", fromDate.ToString() },
                            { "toDate", toDate.ToString() },
                            { "region", region },
                            { "metricName", metricName },
                            { "containerAppArmId", containerAppArmId }
                });
        }

        [Description(@"""
Retrieves times when the billing pod was undergoing leader election in a managed cluster within a time range.
Use this tool to find leader election events for the billing pod.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- PreciseTimeStamp: Timestamp of the leader election event.
"""
)]
        public Task<string> GetBillingPodLeaderElection(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Managed cluster name.")] string managedClusterName
        )
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetBillingPodLeaderElection", region,
                new Dictionary<string, string>
                {
                            { "fromDate", fromDate.ToString() },
                            { "toDate", toDate.ToString() },
                            { "managedClusterName", managedClusterName }
                });
        }

        [Description(@"""
Retrieves times when the VK pod was undergoing leader election in a managed cluster within a time range.
Use this tool to find leader election events for the VK pod.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- TIMESTAMP: Timestamp of the leader election event.
"""
)]
        public Task<string> GetVKPodLeaderElection(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Managed cluster name.")] string managedClusterName
        )
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetVKPodLeaderElection", region,
            new Dictionary<string, string>
            {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "managedClusterName", managedClusterName }
            });
        }

        [Description(@"""
Retrieves AKS Kubelet runtime errors for a managed cluster within a time range.
Use this tool to get Kubelet runtime errors for a specific AKS cluster.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- Timestamp: Timestamp of the error.
- Value: Error value (rate or count).
- region: Azure region.
- cluster_id: Cluster identifier.
- scrape_job: Scrape job name.
- operation_type: Type of Kubelet operation.
"""
)]
        public Task<string> GetAKSKubeletRuntimeErrors(
            [Description("Azure region.")] string regionName,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Resource group name.")] string resourceGroupName,
            [Description("Azure subscription ID.")] string subscriptionId,
            [Description("Managed cluster name.")] string managedClusterName,
            [Description("AKS cluster ID.")] string ccpClusterId
        )
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("GetAKSKubeletRuntimeErrors", "akshuba.centralus", "AKSCCPMetrics",
                new Dictionary<string, string>
                {
                            { "regionName", regionName },
                            { "fromDate", fromDate.ToString() },
                            { "toDate", toDate.ToString() },
                            { "resourceGroupName", resourceGroupName },
                            { "subscriptionId", subscriptionId },
                            { "managedClusterName", managedClusterName },
                            { "ccpClusterId", ccpClusterId }
                });
        }
    }
}
