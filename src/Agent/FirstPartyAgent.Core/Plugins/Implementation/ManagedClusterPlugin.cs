// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;

namespace FirstPartyAgent.Core.Plugins.Implementation;

public class ManagedClusterPlugin : IManagedClusterPlugin
{
    private readonly IKustoPluginChat _kustoPlugin;
    private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;

    public ManagedClusterPlugin(IKustoPluginChat kustoPlugin, IAgentOutboundCommunicationService agentOutboundCommunicationService)
    {
    _kustoPlugin = kustoPlugin;
    _agentOutboundCommunicationService = agentOutboundCommunicationService;
    }

    private static string GetDuration(DateTime fromDate, DateTime toDate)
    {
        var totalHours = (toDate - fromDate).TotalHours;
        var totalDays = (toDate - fromDate).TotalDays;
        // Use the lowest frequency possible for the given range
        if (totalDays > 5)
        {
            return "1d";
        }
        if (totalHours > 24)
        {
            return "1h";
        }
        return "1m";
    }

    public Task<string> GetGenericMetricCountData(string region, DateTime fromDate, DateTime toDate, string managedClusterName, string metricName, int? thresold)
    {

        return _kustoPlugin.ExecuteLocalFunctionAsync("GetGenericMetricCountData", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "managedClusterName", managedClusterName },
            { "metricName", metricName },
            { "duration", GetDuration(fromDate, toDate) },
            { "threshold", thresold.ToString() ?? "0" }
        });
    }

    public Task<string> GetGenericMetricAverageValueData(string region, DateTime fromDate, DateTime toDate, string managedClusterName, string metricName, double? thresold)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetGenericMetricAverageValueData", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "managedClusterName", managedClusterName },
            { "metricName", metricName },
            { "duration", GetDuration(fromDate, toDate) },
            { "threshold", thresold.ToString() ?? "0" }
        });
    }

    public Task<string> GetGenericMetricHistogramPercentilesValueData(string region, DateTime fromDate, DateTime toDate, string managedClusterName, string metricName, double? p50thresold, double? p90thresold, double? p95thresold)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetGenericMetricHistogramPercentilesValueData", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "managedClusterName", managedClusterName },
            { "metricName", metricName },
            { "duration", GetDuration(fromDate, toDate) },
            { "p50Threshold", p50thresold.ToString() ?? "0" },
            { "p90Threshold", p90thresold.ToString() ?? "0" },
            { "p95Threshold", p95thresold.ToString() ?? "0" }
        });
    }

    public async Task<string> GetAksClusterCcpNamespace(string region, DateTime fromDate, DateTime toDate, string resourceGroupName, string subscriptionId, string managedClusterName)
    {
    return await _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("GetAksClusterCcpNamespace", "akshuba.centralus", "AKSprod",
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "resourceGroupName", resourceGroupName },
            { "subscriptionId", subscriptionId },
            { "managedClusterName", managedClusterName },
        });
    }

    public async Task<string> GetASIPageForManagedCLuster(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId)
    {
    var clusterName = await _kustoPlugin.ExecuteFunctionAsync("GetManagedClusterName", region,
        new Dictionary<string, string> {
            { "containerAppNameParam", containerAppName },
            { "resourceGroupParam", resourceGroupName },
            { "subscriptionParam", subscriptionId }
        });

    return await GetASIPageForManagedCluster(region, fromDate, toDate, managedClusterName: clusterName.Result);
    }

    public async Task<string> GetASIPageForManagedCluster(string region, DateTime fromDate, DateTime toDate, string managedClusterName)
    {
        var basePath = "/services/ACA Azure Container Apps/pages/Managed Cluster";
        var cleanPath = Uri.EscapeUriString(basePath); // DO NOT CHANGE TO EscapeDataString

    var query = $"managedClusterName={Uri.EscapeDataString(managedClusterName.Trim())}" +
                $"&globalFrom={Uri.EscapeDataString(fromDate.ToString("M/d/yyyy hh:mm:ss tt"))}" +
                $"&globalTo={Uri.EscapeDataString(toDate.ToString("M/d/yyyy hh:mm:ss tt"))}";

    var adxUri = $"https://asi.azure.ms{cleanPath}?{query}";

    return $"ASI Page for managed cluster {adxUri}";
    }

    public async Task<string> GetSystemComponentErrorEvents(string region, DateTime fromDate, DateTime toDate, string managedClusterName)
    {
    return await _kustoPlugin.ExecuteLocalFunctionAsync("GetSystemComponentErrorEvents", region,
        new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName }
        });
    }
}
