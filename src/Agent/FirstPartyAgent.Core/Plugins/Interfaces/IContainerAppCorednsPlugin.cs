// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace FirstPartyAgent.Core.Plugins.Interfaces;

public interface IContainerAppCorednsPlugin
{
    Task<string> GetCustomDNSServers(string region, DateTime fromDate, DateTime toDate, string managedClusterName);
    Task<string> GetPodFailureEvents(string region, DateTime fromDate, DateTime toDate, string managedClusterName, string podNamePrefix, string podNamespace, int threshold);
    Task<string> GetPodHealthStatus(string region, DateTime fromDate, DateTime toDate, string managedClusterName, string podNamePrefix, string podNamespace);
    Task<string> GetDNSConfigUpdateStatus(string region, DateTime fromDate, DateTime toDate, string managedClusterName);
    Task<string> CheckIfDNSServerFailedToResolveDot(string region, DateTime fromDate, DateTime toDate, string managedClusterName, int threshold);
}
