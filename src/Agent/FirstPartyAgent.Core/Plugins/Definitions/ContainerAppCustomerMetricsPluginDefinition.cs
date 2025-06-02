using System.ComponentModel;
using FirstPartyAgent.Constants;
using FirstPartyAgent.Core.Plugins.Interfaces;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.Plugins.Definitions
{
    public class ContainerAppCustomerMetricsPluginDefinition(IContainerAppCustomerMetricsPlugin Plugin)
    {
        private readonly IContainerAppCustomerMetricsPlugin _metricsAgentPlugin = Plugin;

        [KernelFunction(KernelFunctionNames.ACA.GetContainerAppInfraLayer)]
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
            return _metricsAgentPlugin.GetContainerAppInfraLayer(region, fromDate, toDate, subscriptionId, resourceGroupName, containerAppName, managedClusterName);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetMetricsMdmCount)]
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
            return _metricsAgentPlugin.GetMetricsMdmCount(region, fromDate, toDate, metricName, containerAppArmId);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetMdmPodHeartbeatMissedTimes)]
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
            return _metricsAgentPlugin.GetMdmPodHeartbeatMissedTimes(region, fromDate, toDate, managedClusterName);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetMissedMdmMetricTimes)]
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
            return _metricsAgentPlugin.GetMissedMdmMetricTimes(region, fromDate, toDate, metricName, containerAppArmId);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetBillingPodLeaderElection)]
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
            return _metricsAgentPlugin.GetBillingPodLeaderElection(region, fromDate, toDate, managedClusterName);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetVKPodLeaderElection)]
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
            return _metricsAgentPlugin.GetVKPodLeaderElection(region, fromDate, toDate, managedClusterName);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetAKSKubeletRuntimeErrors)]
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
            return _metricsAgentPlugin.GetAKSKubeletRuntimeErrors(regionName, fromDate, toDate, resourceGroupName, subscriptionId, managedClusterName, ccpClusterId);
        }
    }

}
