using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;

namespace FirstPartyAgent.Core.Plugins.Implementation
{
    public class ContainerAppCustomerMetricsPlugin : IContainerAppCustomerMetricsPlugin
    {
        private readonly IKustoPluginChat _kustoPlugin;
        public ContainerAppCustomerMetricsPlugin(IKustoPluginChat kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }

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

        public Task<string> GetAKSKubeletRuntimeErrors(string regionName, DateTime fromDate, DateTime toDate, string resourceGroupName, string subscriptionId, string managedClusterName, string ccpClusterId)
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

        public Task<string> GetVKPodLeaderElection(string region, DateTime fromDate, DateTime toDate, string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetVKPodLeaderElection", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName }
                });
        }
    }
}
