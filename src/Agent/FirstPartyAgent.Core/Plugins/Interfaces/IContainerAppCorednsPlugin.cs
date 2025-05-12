// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace FirstPartyAgent.Core.Plugins.Interfaces;

// [MENDATORY]
public interface IContainerAppCorednsPlugin
{
    Task<string> GetCustomDNSServers(string region, DateTime fromDate, DateTime toDate, string managedClusterName);
    Task<string> GetCoreDNSCountMetricData(string region, DateTime fromDate, DateTime toDate, string managedClusterName, string metricName, int thresold);
    Task<string> GetCoreDNSAvgLatencyMetricData(string region, DateTime fromDate, DateTime toDate, string managedClusterName, string metricName, int thresold);
    Task<string> GetPodFailureEvents(string region, DateTime fromDate, DateTime toDate, string managedClusterName, string podNamePrefix, string podNamespace, int threshold);
    Task<string> GetPodHealthStatus(string region, DateTime fromDate, DateTime toDate, string managedClusterName, string podNamePrefix, string podNamespace);
    Task<string> GetDNSConfigUpdateStatus(string region, DateTime fromDate, DateTime toDate, string managedClusterName);
    Task<string> CheckIfDNSServerFailedToResolveDot(string region, DateTime fromDate, DateTime toDate, string managedClusterName, int threshold);
}
