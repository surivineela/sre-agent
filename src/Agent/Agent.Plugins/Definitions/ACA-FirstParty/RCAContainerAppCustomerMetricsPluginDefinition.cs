// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Framework;
using Agent.Plugins.Interface;
using Agent.Plugins.Kusto;
using Agent.Plugins.KustoPlugin;
using Agent.Plugins.Services.Interfaces;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(IsFirstPartyOnly = true)]
    public class RCAContainerAppCustomerMetricsPluginDefinition
    {
        private readonly IKustoPlugin _kustoPlugin;
        public RCAContainerAppCustomerMetricsPluginDefinition(IKustoPlugin kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }

        [Description(@"""
        Purpose:
        Retrieves the underlying infrastructure layer (AKS or Legion) for a container app.

        Scenario:
        Use this tool to determine the underlying infrastructure layer for a container app in a specific region and time range.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - InfraLayer: The infrastructure layer, either 'AKS' or 'Legion'
        """
        )]
        public Task<string> GetContainerAppInfraLayer(
            [Description("Azure region.")] AzureRegion region,
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
                            { "region", region.ToNormalizedString() },
                            { "subscriptionId", subscriptionId },
                            { "resourceGroupName", resourceGroupName },
                            { "containerAppName", containerAppName },
                            { "managedClusterName", managedClusterName }
                });
        }

        [Description(@"""
        Purpose:
        Checks if metrics were missed for a container app or job in the specified time range.

        Scenario:
        Use this tool to when investigating missing metrics issues for a given metric name and container app.
        This tool checks if the metric was missed for a given time frame

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - MetricsMissed: 'True' if metrics were missed, otherwise 'False'
        """
        )]
        public Task<string> GetMetricsMdmCount(
            [Description("Azure region.")] AzureRegion region,
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
                        { "region", region.ToNormalizedString() },
                        { "metricName", metricName },
                        { "containerAppArmId", containerAppArmId }
             });
        }

        [Description(@"""
        Purpose:
        Retrieves missed times for MDM pod heartbeats in a managed cluster within a time range.

        Scenario:
        Use this tool when investigating missing metrics issues and potential issues with the MDM pod.
        This tool identifies times when the MDM pod hearbeats are not reported.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - PreciseTimeStamp: Timestamp when a heartbeat was missed
        """
        )]
        public Task<string> GetMdmPodHeartbeatMissedTimes(
            [Description("Azure region.")] AzureRegion region,
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
        Purpose:
        Retrieves times when a specific metric was missed for a container app in the specified time range.

        Scenario:
        Use this tool when investigating missing metrics issues for a given metric name and container app.
        This tool retrieves the time when the specified metric was missed.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - TimestampUtc: Timestamp when the metric was missed
        """
        )]
        public Task<string> GetMissedMdmMetricTimes(
            [Description("Azure region.")] AzureRegion region,
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
                            { "region", region.ToNormalizedString() },
                            { "metricName", metricName },
                            { "containerAppArmId", containerAppArmId }
                });
        }

        [Description(@"""
        Purpose:
        Retrieves times when the billing pod was undergoing leader election in a managed cluster within a time range.

        Scenario:
        Use this tool when investigating metrics loss issues.
        When the billing pod is undergoing leader election, it may not report metrics.
        This tool identifies times when the billing pod was undergoing leader election.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - PreciseTimeStamp: Timestamp of the leader election event
        """
        )]
        public Task<string> GetBillingPodLeaderElection(
            [Description("Azure region.")] AzureRegion region,
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
        Purpose:
        Retrieves times when the VK (Virtual Kubelet) pod was undergoing leader election in a managed cluster within a time range.

        Scenario:
        Use this tool when investigating metrics loss issues.
        When the VK pod is undergoing leader election, it may not report metrics for Container apps and Jobs running on Legion..
        Use this tool to find leader election events for the VK (Virtual Kubelet) pod.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - TIMESTAMP: Timestamp of the leader election event
        """
        )]
        public Task<string> GetVKPodLeaderElection(
            [Description("Azure region.")] AzureRegion region,
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
        Purpose:
        Retrieves AKS Kubelet runtime errors for a managed cluster within a time range.

        Scenario:
        Use this tool when invvestigating metrics loss issues for Container Apps and Jobs running on AKS.
        Kubelet runtime errors can indicate issues with the AKS cluster's kubelet operations.
        Use this tool to get Kubelet runtime errors for a specific AKS cluster.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - Timestamp: Timestamp of the error
        - Value: Error value (rate or count)
        - Region: Azure region
        - AKSClusterID: Cluster identifier
        - AKSScrapeJob: Scrape job name
        - OperationType: Type of Kubelet operation
        """
        )]
        public Task<string> GetAKSKubeletRuntimeErrors(
            [Description("Azure region.")] AzureRegion region,
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
                            { "regionName", region.ToNormalizedString() },
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
