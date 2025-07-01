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

        [Description(@"
This operation will get if the container app CPU percentage is above specified threshold in the duration specified by fromDate and toDate.

Input parameters:
- region: The Azure region where the container app is hosted
- fromDate: The start date for the query
- toDate: The end date for the query
- metricName: The name of the metric to check
- containerAppArmId: The ARM ID of the container app
- samplingType: The type of sampling to use (e.g., 'Max', 'Average', 'Min')
- Threshold: The threshold value to compare against the metric (e.g., '80' for 80% CPU usage)

Output:
Returns true if the CPU percentage is above the specified threshold, otherwise false.")]
        public Task<string> GetContainerAppCpuExceedsThreshold(string region, DateTime fromDate, DateTime toDate, string containerAppArmId, string samplingType, string Threshold)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetMdmResult", region,
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

        [Description(@"
This operation will get if the container app memory percentage is above specified threshold in the duration specified by fromDate and toDate.

Input parameters:
- region: The Azure region where the container app is hosted
- fromDate: The start date for the query
- toDate: The end date for the query
- metricName: The name of the metric to check
- containerAppArmId: The ARM ID of the container app
- samplingType: The type of sampling to use (e.g., 'Max', 'Average', 'Min')
- Threshold: The threshold value to compare against the metric (e.g., '80' for 80% CPU usage)

Output:
Returns true if the Memory percentage is above the specified threshold, otherwise false.")]
        public Task<string> GetContainerAppMemoryExceedsThreshold(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Relative Url specifying the ARM Id for container app. example: '/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.App/containerApps/{ContainerAppName}'")] string containerAppArmId,
            [Description("Metric sampling type. example: 'Max', 'Average, 'Min'")] string samplingType,
            [Description("Threshold as a percentage to check if Metric equals or exceeds. example: '90'")] string Threshold)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetMdmResult", region,
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

    [Description(@"
Retrieve Out of Memory (OOM) kill events for container apps within a managed cluster.This operation identifies instances where containers were terminated due to memory resource exhaustion.
OOM kills indicate that a container exceeded its memory limits or that the node ran out of available memory.

Input parameters:
- region: The Azure region where the container app is hosted
- fromDate: The start date for the query
- toDate: The end date for the query
- managedClusterName: Name of the managed Kubernetes cluster
- containerAppOrJobName: Name of the container app or job (use empty string if not available)

Output:
- PreciseTimeStamp
- ManagedClusterNam
- ContainerAppName
- RevisionName
- ReplicaName
- Count
- resourceId")]
        public Task<string> GetContainerAppOOMKills(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName,
            [Description("Name of the container app or job. Use empty string if container app or job name is not available")] string containerAppOrJobName)
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
