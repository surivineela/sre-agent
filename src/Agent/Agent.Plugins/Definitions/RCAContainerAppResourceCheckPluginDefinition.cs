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
    public class RCAContainerAppResourceCheckPluginDefinition
    {
        private readonly IKustoPluginChat _kustoPlugin;
        public RCAContainerAppResourceCheckPluginDefinition(IKustoPluginChat kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }

        [Description(@"""
        Purpose:
        Checks if the container app CPU usage exceeds a specified threshold during a given time window.

        Scenario:
        Use this tool to determine if CPU usage is above a threshold for a container app.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - hasRows: true if CPU usage exceeded the threshold, false otherwise
        """
        )]
        public Task<string> GetContainerAppCpuExceedsThreshold(
            [Description("Azure region in lower case. Example: 'westeurope'.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Relative URL specifying the ARM ID for the container app. Example: '/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.App/containerApps/{ContainerAppName}'.")] string containerAppArmId,
            [Description("Metric sampling type. Example: 'Max', 'Average', 'Min'.")] string samplingType,
            [Description("Threshold as a percentage to check if metric equals or exceeds. Example: '80'.")] string Threshold)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetSparseMdm", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "region", region },
                    { "metricName", "CpuPercentage" },
                    { "samplingType", samplingType },
                    { "threshold", Threshold },
                    { "containerAppArmId", containerAppArmId }
                });
        }

        [Description(@"""
        Purpose:
        Checks if the container app memory usage exceeds a specified threshold during a given time window.

        Scenario:
        Use this tool to determine if memory usage is above a threshold for a container app.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - hasRows: true if memory usage exceeded the threshold, false otherwise
        """
        )]
        public Task<string> GetContainerAppMemoryExceedsThreshold(
            [Description("Azure region in lower case. Example: 'westeurope'.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Relative URL specifying the ARM ID for the container app. Example: '/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.App/containerApps/{ContainerAppName}'.")] string containerAppArmId,
            [Description("Metric sampling type. Example: 'Max', 'Average', 'Min'.")] string samplingType,
            [Description("Threshold as a percentage to check if metric equals or exceeds. Example: '90'.")] string Threshold)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetSparseMdm", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "region", region },
                    { "metricName", "MemoryPercentage" },
                    { "samplingType", samplingType },
                    { "threshold", Threshold },
                    { "containerAppArmId", containerAppArmId }
                });
        }

            [Description(@"""
        Purpose:
        Retrieves OOM (Out Of Memory) kill events for a container app or job in a managed cluster during a given time window.

        Scenario:
        Use this tool when container app or job pods are crashing unexpectedly to identify if pods are being killed due to memory issue.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - PreciseTimeStamp: Time when the OOM kill event occurred
        - ManagedClusterName: Name of the managed cluster
        - ContainerAppName: Name of the container app
        - RevisionName: Name of the revision
        - ReplicaName: Name of the replica
        - Count: Number of OOM kill events
        - resourceId: Resource ID of the container app or job
        """
        )]
        public Task<string> GetContainerAppOrJobOOMKills(
            [Description("Azure region in lower case. Example: 'westeurope'.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName,
            [Description("Name of the container app or job. Use empty string if not available.")] string containerAppOrJobName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetEventProcessorOOMKills", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName },
                    { "containerAppOrJobName", containerAppOrJobName }
                });
        }
    }
}
