// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace FirstPartyAgent.Core.Plugins.Implementation;

// [MENDATORY]
public class ContainerAppCorednsPlugin : IContainerAppCorednsPlugin
{
    private readonly IKustoPluginChat _kustoPlugin;

    public ContainerAppCorednsPlugin(IKustoPluginChat kustoPlugin)
    {
        _kustoPlugin = kustoPlugin;
    }

    public async Task<string> CheckIfCustomDNSConfigured(string region, DateTime fromDate, DateTime toDate, string managedClusterName)
    {
        string query = $@"
            let fromDate = datetime(""{fromDate}"");
            let endDate = datetime(""{toDate}"");
            let environmentName =  ""{managedClusterName}"";
            SwiftNetworkingEvents
            | where TIMESTAMP between (fromDate .. endDate)
            | where EnvironmentName == environmentName
            | where msg has ""Customer DNS Servers are >> ""
            | summarize emptyDNSServersCount = countif(msg == ""Customer DNS Servers are >> ""), totalCount = count()
            | project  isCustomDNSConfigured = case(
                totalCount > 0 and emptyDNSServersCount == totalCount, ""False"",
                totalCount > 0 and emptyDNSServersCount == 0, ""True"",
                totalCount == 0, ""Data unavailable"",
                ""Unknown"" // Unknown means DNS configuration changed in the time interval
            )
            ";
        return (await _kustoPlugin.ExecuteKustoQuery(region, query)).Result;
    }

    public async Task<string> GetCustomDNSServers(string region, DateTime fromDate, DateTime toDate, string managedClusterName)
    {
        string query = $@"
                let fromDate = datetime(""{fromDate}"");
                let endDate = datetime(""{toDate}"");
                let environmentName =  ""{managedClusterName}"";
                SwiftNetworkingEvents
                | where PreciseTimeStamp  between (fromDate .. endDate)
                | where EnvironmentName == environmentName
                | where msg has ""Customer DNS Servers are >> ""
                | summarize StartTime = min(PreciseTimeStamp), EndTime = max(PreciseTimeStamp) by msg
                ";
        return (await _kustoPlugin.ExecuteKustoQuery(region, query)).Result;
    }

    public Task<string> GetCoreDNSCountMetricData(string region, DateTime fromDate, DateTime toDate, string managedClusterName, string metricName, int thresold)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetCoreDNSCountMetricData", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "managedClusterName", managedClusterName },
            { "metricName", metricName },
            { "duration", "1d" },
            { "threshold", thresold.ToString() }
        });
    }

    public Task<string> GetCoreDNSAvgLatencyMetricData(string region, DateTime fromDate, DateTime toDate, string managedClusterName, string metricName, int thresold)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetCoreDNSAverageLatencyMetricData", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "managedClusterName", managedClusterName },
            { "metricName", metricName },
            { "duration", "1d" },
            { "threshold", thresold.ToString() }
        });
    }

    public async Task<string> GetMyCoreDNSAvgLatencyMetricDataAsync(string region, DateTime fromDate, DateTime toDate, string managedClusterName, string metricName, int thresold)
    {
        var rawData = await _kustoPlugin.ExecuteLocalFunctionAsync("GetCoreDNSAverageLatencyMetricData", region,
            new Dictionary<string, string>
            {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "duration", "1d" },
                { "metricName", metricName },
                { "threshold", thresold.ToString() }
            });
        return rawData;
    }

    public Task<string> GetPodFailureEvents(string region, DateTime fromDate, DateTime toDate, string managedClusterName, string podNamePrefix, string podNamespace, int threshold)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetPodFailureEvents", region,
    new Dictionary<string, string>
    {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "threshold", threshold.ToString() },
                { "podNamePrefix", podNamePrefix },
                { "podNamespace", podNamespace }
    });
    }

    public Task<string> GetPodHealthStatus(string region, DateTime fromDate, DateTime toDate, string managedClusterName, string podNamePrefix, string podNamespace)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetPodHealthStatus", region,
    new Dictionary<string, string>
    {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "podNamePrefix", podNamePrefix },
                { "podNamespace", podNamespace }
    });
    }

    public Task<string> GetDNSConfigUpdateStatus(string region, DateTime fromDate, DateTime toDate, string managedClusterName)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetDNSConfigUpdateStatus", region,
    new Dictionary<string, string>
    {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName }
    });
    }

    public Task<string> CheckIfDNSServerFailedToResolveDot(string region, DateTime fromDate, DateTime toDate, string managedClusterName, int threshold)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("CheckIfDNSServerFailedToResolveDot", region,
    new Dictionary<string, string>
    {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "threshold", threshold.ToString() }
    });
    }

}
