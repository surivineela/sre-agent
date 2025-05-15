// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;

namespace FirstPartyAgent.Core.Plugins.Implementation;
public class ContainerAppEnvoyPlugin : IContainerAppEnvoyPlugin
{
    private readonly IKustoPluginChat _kustoPlugin;

    public ContainerAppEnvoyPlugin(IKustoPluginChat kustoPlugin)
    {
        _kustoPlugin = kustoPlugin;
    }

    public Task<string> GetContainerAppManagedCluster(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetContainerAppManagedCluster", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "subscriptionId", subscriptionId },
            { "resourceGroupName", resourceGroupName },
            { "containerAppName", containerAppName }
        });
    }

    public Task<string> GetEnvoyPodLogs(string region, DateTime fromDate, DateTime toDate, string managedClusterName)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetEnvoyPodLogs", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "managedClusterName", managedClusterName }
        });
    }

    public Task<string> GetEnvoyControllerLogs(string region, DateTime fromDate, DateTime toDate, string managedClusterName)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetEnvoyControllerLogs", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "managedClusterName", managedClusterName }
        });
    }


    public Task<string> GetEnvoyAccessLogs(string region, DateTime fromDate, DateTime toDate, string managedClusterName)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetEnvoyAccessLogs", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "managedClusterName", managedClusterName }
        });
    }

    public Task<string> GetSwiftNetworkingEvents(string region, DateTime fromDate, DateTime toDate, string managedClusterName)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetSwiftNetworkingEvents", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "managedClusterName", managedClusterName }
        });
    }

    public Task<string> GetEnvoyPodStatus(string region, DateTime fromDate, DateTime toDate, string managedClusterName)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetPodHealthStatus", region,
        new Dictionary<string, string>
        {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "managedClusterName", managedClusterName },
            { "podNamePrefix", "k8se-envoy" },
            { "podNamespace", "k8se-system" }
        });
    }

    public Task<string> GetCustomerAppPodStatus(string region, DateTime fromDate, DateTime toDate, string managedClusterName, string containerAppName)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetPodHealthStatus", region,
        new Dictionary<string, string>
        {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "managedClusterName", managedClusterName },
            { "podNamePrefix", containerAppName },
            { "podNamespace", "k8se-apps" }
        });
    }

    public Task<string> GetContainerAppStatus(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetContainerAppStatus", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "subscriptionId", subscriptionId },
            { "resourceGroupName", resourceGroupName },
            { "containerAppName", containerAppName }
        });
    }

    public Task<string> GetContainerAppAdminEvents(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetContainerAppAdminEvents", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "subscriptionId", subscriptionId },
            { "resourceGroupName", resourceGroupName },
            { "containerAppName", containerAppName }
        });
    }
}
