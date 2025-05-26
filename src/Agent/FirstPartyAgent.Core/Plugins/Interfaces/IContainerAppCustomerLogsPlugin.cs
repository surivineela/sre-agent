// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace FirstPartyAgent.Plugins
{
    public interface IContainerAppCustomerLogsPlugin
    {
        Task<string> GetLogConfiguration(string region, DateTime fromDate, DateTime toDate, Guid customerSubscriptionId, string managedEnvironmentName, string managedClusterName);

        Task<string> GetEventProcessorErrors(string region, DateTime fromDate, DateTime toDate, string managedClusterName, string containerAppOrJobName);

        Task<string> GetEventProcessorLeaderElectionEvents(string region, DateTime fromDate, DateTime toDate, string managedClusterName);

        Task<string> GetAppsAndjobsVolumeForEnv(string region, DateTime fromDate, DateTime toDate, string managedClusterName);

        Task<string> GetEventProcessorPods(string region, DateTime fromDate, DateTime toDate, string managedClusterName);

        Task<string> GetLogProcessorPods(string region, DateTime fromDate, DateTime toDate, string managedClusterName);

        Task<string> GetEventProcessorPodStatus(string region, DateTime fromDate, DateTime toDate, string managedClusterName);

        Task<string> GetLogProcessorPodStatus(string region, DateTime fromDate, DateTime toDate, string managedClusterName);

        Task<string> GetContainerAppWorkloadProfile(string region, DateTime fromDate, DateTime toDate, string containerAppOrJobName);
    }
}
