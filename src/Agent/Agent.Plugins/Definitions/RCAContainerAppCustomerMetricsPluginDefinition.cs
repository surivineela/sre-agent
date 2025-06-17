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

        [Description(@"
This operation will get the underlying infrastucture for the customer's container app

Input parameters:
- region: The Azure region where the container app is hosted
- fromDate: The start date for the query
- toDate: The end date for the query
- subscriptionId: The Id of the Azure subscription
- resourceGroupName: The name of the resource group where the container app is hosted
- containerAppName: The name of the container app
- managedClusterName: The name of the managed cluster

Output:
The return value will be either AKS or Legion, which is the underlying infrastructure layer")]
        public Task<string> GetContainerAppInfraLayer(string region, DateTime fromDate, DateTime toDate, string subscriptionId, string resourceGroupName, string containerAppName, string managedClusterName)
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

        [Description(@"
This operation identifies whether metrics were missed in the given time period

Input parameters:
- region: The Azure region where the container app is hosted
- fromDate: The start date for the query
- toDate: The end date for the query
- metricName: The name of the metric to check
- containerAppArmId: The ARM ID of the container app

Returns true if the metric is missing, otherwise false.
")]
        public Task<string> GetMetricsMdmCount(
            string region,
            DateTime fromDate,
            DateTime toDate,
            string metricName,
            string containerAppArmId
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

        [Description(@"
This operation retrieves the missed times for MDM pod heartbeats in the specified time range.

Input parameters:
- region: The Azure region where the container app is hosted
- fromDate: The start date for the query
- toDate: The end date for the query
- managedClusterName: The name of the managed cluster

Returns a string containing the missed times for MDM pod heartbeats in the specified time range.
")]
        public Task<string> GetMdmPodHeartbeatMissedTimes(
            string region,
            DateTime fromDate,
            DateTime toDate,
            string managedClusterName
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

        [Description(@"
This operation retrieves times where metrics were missed in the specified time range.

Input parameters:
- region: The Azure region where the container app is hosted
- fromDate: The start date for the query
- toDate: The end date for the query
- metricName: The name of the metric to check
- containerAppArmId: The ARM ID of the container app

Returns a string containing the times where the specified metric was missed in the given time range. If empty, the metric was not missed.
")]
        public Task<string> GetMissedMdmMetricTimes(
            string region,
            DateTime fromDate,
            DateTime toDate,
            string metricName,
            string containerAppArmId
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

        [Description(@"
This operation retrieves times when the billing pod was going through a leader election in the specified time range.

Input parameters:
- region: The Azure region where the container app is hosted
- fromDate: The start date for the query
- toDate: The end date for the query
- managedClusterName: The name of the managed cluster

Returns a string containing the times when the billing pod was going through a leader election in the specified time range. If empty, there were no leader elections.
")]
        public Task<string> GetBillingPodLeaderElection(
            string region,
            DateTime fromDate,
            DateTime toDate,
            string managedClusterName
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

        [Description(@"
This operation retrieves times when the VK pod was going through a leader election in the specified time range.

Input parameters:
- region: The Azure region where the container app is hosted
- fromDate: The start date for the query
- toDate: The end date for the query
- managedClusterName: The name of the managed cluster

Returns a string containing the times when the VK pod was going through a leader election in the specified time range. If empty, there were no leader elections.
")]
        public Task<string> GetVKPodLeaderElection(
            string region,
            DateTime fromDate,
            DateTime toDate,
            string managedClusterName
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

        [Description(@"
This operation retrives AKS Kubelet runtime errors in the specified time range.

Input parameters:
- regionName: The Azure region where the AKS cluster is hosted
- fromDate: The start date for the query
- toDate: The end date for the query
- resourceGroupName: The name of the resource group hosting the AKS cluster
- subscriptionId: The Azure subscription ID
- managedClusterName: The name of the managed cluster
- ccpClusterId: The AKS Cluster Id, which consists of only numbers or letters (e.g., 666b5141d2007500010d60f3)

Returns a string containing the AKS Kubelet runtime errors in the specified time range. If empty, there were no Kubelet runtime errors.
")]
        public Task<string> GetAKSKubeletRuntimeErrors(
            string regionName,
            DateTime fromDate,
            DateTime toDate,
            string resourceGroupName,
            string subscriptionId,
            string managedClusterName,
            string ccpClusterId
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
