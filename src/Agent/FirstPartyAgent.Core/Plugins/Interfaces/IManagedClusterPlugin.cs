// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using FirstPartyAgent.Plugins;

namespace FirstPartyAgent.Core.Plugins.Interfaces;

public interface IManagedClusterPlugin
{
    Task<string> GetGenericMetricCountData(string region, DateTime fromDate, DateTime toDate, string managedClusterName, string metricName, int? thresold);
    Task<string> GetGenericMetricAverageValueData(string region, DateTime fromDate, DateTime toDate, string managedClusterName, string metricName, double? thresold);
    Task<string> GetGenericMetricHistogramPercentilesValueData(string region, DateTime fromDate, DateTime toDate, string managedClusterName, string metricName, double? p50thresold, double? p90thresold, double? p95thresold);

    Task<string> GetASIPageForManagedCLuster(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId);
    Task<string> GetASIPageForManagedCluster(string region, DateTime fromDate, DateTime toDate, string managedClusterName);
    Task<string> GetAksClusterCcpNamespace(string region, DateTime fromDate, DateTime toDate, string resourceGroupName, string subscriptionId, string managedClusterName);
}
