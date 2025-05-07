// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace FirstPartyAgent.Core.Plugins.Interfaces;
public interface IContainerAppEnvoyPlugin
{
    Task<string> GetContainerAppManagedCluster(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId);
    Task<string> GetEnvoyAbnormalLogs(string region, DateTime fromDate, DateTime toDate, string managedClusterName);
    Task<string> GetEnvoyControllerLogs(string region, DateTime fromDate, DateTime toDate, string managedClusterName);
    Task<string> GetEnvoyAccessLogs(string region, DateTime fromDate, DateTime toDate, string managedClusterName);
    Task<string> GetSwiftNetworkingEvents(string region, DateTime fromDate, DateTime toDate, string managedClusterName);
    Task<string> GetEnvoyPodStatus(string region, DateTime fromDate, DateTime toDate, string managedClusterName);
    Task<string> GetEnvoyControllerPodStatus(string region, DateTime fromDate, DateTime toDate, string managedClusterName);
    Task<string> GetContainerAppStatus(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId);
    Task<string> GetContainerAppAdminEvents(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId);
}
