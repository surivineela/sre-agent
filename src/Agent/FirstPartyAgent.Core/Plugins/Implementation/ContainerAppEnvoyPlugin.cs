// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;

namespace FirstPartyAgent.Core.Plugins.Implementation;
public class ContainerAppEnvoyPlugin : IContainerAppEnvoyPlugin
{
    private readonly IKustoPlugin _kustoPlugin;

    public ContainerAppEnvoyPlugin(IKustoPlugin kustoPlugin)
    {
        _kustoPlugin = kustoPlugin;
    }

    public Task<string> GetEnvoyAbnormalLogs(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetEnvoyAbnormalLogs", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "subscriptionId", subscriptionId },
            { "resourceGroupName", resourceGroupName },
            { "containerAppName", containerAppName }
        });
    }

    public Task<string> GetEnvoyControllerLogs(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetEnvoyControllerLogs", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "subscriptionId", subscriptionId },
            { "resourceGroupName", resourceGroupName },
            { "containerAppName", containerAppName }
        });
    }


    public Task<string> GetEnvoyAccessLogs(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetEnvoyAccessLogs", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "subscriptionId", subscriptionId },
            { "resourceGroupName", resourceGroupName },
            { "containerAppName", containerAppName }
        });
    }

    public Task<string> GetSwiftNetworkingEvents(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetSwiftNetworkingEvents", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "subscriptionId", subscriptionId },
            { "resourceGroupName", resourceGroupName },
            { "containerAppName", containerAppName }
        });
    }
}
