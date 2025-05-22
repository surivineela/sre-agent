using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FirstPartyAgent.Constants;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.Plugins.Implementation
{
    public class ContainerAppCustomerMetricsPlugin : IContainerAppCustomerMetricsPlugin
    {
        private readonly IKustoPluginChat _kustoPlugin;
        public ContainerAppCustomerMetricsPlugin(IKustoPluginChat kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }

        [KernelFunction(KernelFunctionNames.ACA.GetMetricsMdmCount)]
        public Task<string> GetMetricsMdmCount(string region, DateTime fromDate, DateTime toDate, string metricName, string containerAppArmId)
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

        [KernelFunction(KernelFunctionNames.ACA.GetMdmPodHeartbeatMissedTimes)]
        public Task<string> GetMdmPodHeartbeatMissedTimes(string region, DateTime fromDate, DateTime toDate, string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetMdmPodHeartbeatMissedTimes", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName}
                });
        }

        [KernelFunction(KernelFunctionNames.ACA.GetMissedMdmMetricTimes)]
        public Task<string> GetMissedMdmMetricTimes(string region, DateTime fromDate, DateTime toDate, string metricName, string containerAppArmId)
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

        [KernelFunction(KernelFunctionNames.ACA.GetBillingPodLeaderElection)]
        public Task<string> GetBillingPodLeaderElection(string region, DateTime fromDate, DateTime toDate, string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetBillingPodLeaderElection", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName }
                });
        }
    }
}
