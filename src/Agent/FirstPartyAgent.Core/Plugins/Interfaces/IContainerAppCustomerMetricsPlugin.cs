namespace FirstPartyAgent.Core.Plugins.Interfaces
{
    public interface IContainerAppCustomerMetricsPlugin
    {
        Task<string> GetContainerAppInfraLayer(string region, DateTime fromDate, DateTime toDate, string subscriptionId, string resourceGroupName, string containerAppName, string managedClusterName);
        Task<string> GetMetricsMdmCount(string region, DateTime fromDate, DateTime toDate, string metricName, string containerAppArmId);

        Task<string> GetMdmPodHeartbeatMissedTimes(string region, DateTime fromDate, DateTime toDate, string managedClusterName);

        Task<string> GetMissedMdmMetricTimes(string region, DateTime fromDate, DateTime toDate, string metricName, string containerAppArmId);

        Task<string> GetBillingPodLeaderElection(string region, DateTime fromDate, DateTime toDate, string managedClusterName);
        Task<string> GetVKPodLeaderElection(string region, DateTime fromDate, DateTime toDate, string managedClusterName);
        Task<string> GetAKSKubeletRuntimeErrors(string regionName, DateTime fromDate, DateTime toDate, string resourceGroupName, string subscriptionId, string managedClusterName, string ccpClusterId);
    }
}
