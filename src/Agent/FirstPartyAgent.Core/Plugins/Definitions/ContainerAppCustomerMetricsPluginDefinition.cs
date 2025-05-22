using System.ComponentModel;
using FirstPartyAgent.Constants;
using FirstPartyAgent.Core.Plugins.Interfaces;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.Plugins.Definitions
{
    public class ContainerAppCustomerMetricsPluginDefinition(IContainerAppCustomerMetricsPlugin Plugin)
    {
        private readonly IContainerAppCustomerMetricsPlugin _metricsAgentPlugin = Plugin;

        [KernelFunction(KernelFunctionNames.ACA.GetMetricsMdmCount)]
        [Description(@"Get Count aggregation for metrics")]
        public Task<string> GetMetricsMdmCount(string region, DateTime fromDate, DateTime toDate, string metricName, string containerAppArmId)
        {
            return _metricsAgentPlugin.GetMetricsMdmCount(region, fromDate, toDate, metricName, containerAppArmId);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetMdmPodHeartbeatMissedTimes)]
        [Description(@"Get MDM Pod Hearbeat Missed Times")]
        public Task<string> GetMdmPodHeartbeatMissedTimes(string region, DateTime fromDate, DateTime toDate, string managedClusterName)
        {
            return _metricsAgentPlugin.GetMdmPodHeartbeatMissedTimes(region, fromDate, toDate, managedClusterName);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetMissedMdmMetricTimes)]
        [Description(@"Get Missed MDM Metric Times")]
        public Task<string> GetMissedMdmMetricTimes(string region, DateTime fromDate, DateTime toDate, string metricName, string containerAppArmId)
        {
            return _metricsAgentPlugin.GetMissedMdmMetricTimes(region, fromDate, toDate, metricName, containerAppArmId);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetBillingPodLeaderElection)]
        [Description(@"Get Billing Pod Metric Emission Gaps")]
        public Task<string> GetBillingPodLeaderElection(string region, DateTime fromDate, DateTime toDate, string managedClusterName)
        {
            return _metricsAgentPlugin.GetBillingPodLeaderElection(region, fromDate, toDate, managedClusterName);
        }
    }

}
