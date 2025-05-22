// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;

namespace FirstPartyAgent.Core.Plugins.Implementation;

public class ContainerAppCorednsPlugin : IContainerAppCorednsPlugin
{
    private readonly IKustoPluginChat _kustoPlugin;

    public ContainerAppCorednsPlugin(IKustoPluginChat kustoPlugin)
    {
        _kustoPlugin = kustoPlugin;
    }

    public Task<string> GetCustomDNSServers(string region, DateTime fromDate, DateTime toDate, string managedClusterName)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetCustomDNSServersOverTime", region,
         new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName }
         });
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
