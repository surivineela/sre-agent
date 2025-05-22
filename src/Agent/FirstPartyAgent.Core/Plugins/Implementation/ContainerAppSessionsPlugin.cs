// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;

namespace FirstPartyAgent.Core.Plugins.Implementation;

public class ContainerAppSessionsPlugin : IContainerAppSessionsPlugin
{
    private readonly IKustoPluginChat _kustoPlugin;

    public ContainerAppSessionsPlugin(IKustoPluginChat kustoPlugin)
    {
        _kustoPlugin = kustoPlugin;
    }

    public Task<string> GetSessionPoolInfo(string region, DateTime fromDate, DateTime toDate, string subscriptionId, string resourceGroupName, string sessionPoolName)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetSessionPoolInfo", region,
         new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "subscriptionId", subscriptionId },
                { "resourceGroupName", resourceGroupName },
                { "sessionPoolName", sessionPoolName }
         });
    }

    public Task<string> GetChangesInSessionPool(string region, DateTime fromDate, DateTime toDate, string subscriptionId, string resourceGroupName, string sessionPoolName)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetChangesInSessionPool", region,
         new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "subscriptionId", subscriptionId },
                { "resourceGroupName", resourceGroupName },
                { "sessionPoolName", sessionPoolName }
         });
    }

    public Task<string> GetSessionPodLogs(string region, DateTime fromDate, DateTime toDate, string podName)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetSessionPodLogs", region,
         new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "podName", podName }
         });
    }

    public Task<string> GetSessionPoolCreateOrUpdateLogs(string region, DateTime fromDate, DateTime toDate, string subscriptionId, string resourceGroupName, string sessionPoolName)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetSessionPoolCreateOrUpdateLogs", region,
         new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "subscriptionId", subscriptionId },
                { "resourceGroupName", resourceGroupName },
                { "sessionPoolName", sessionPoolName }
         });
    }

    public Task<string> GetCodeInterpreterSessionExecutionEventLogs(string region, DateTime fromDate, DateTime toDate, string subscriptionId, string resourceGroupName, string sessionPoolName, string sessionIdentifier = "")
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetCodeInterpreterSessionExecutionEventLogs", region,
         new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "subscriptionId", subscriptionId },
                { "resourceGroupName", resourceGroupName },
                { "sessionPoolName", sessionPoolName },
                { "sessionIdentifier", sessionIdentifier }
         });
    }
    
    public Task<string> GetCustomContainerSessionActivatorLogs(string region, DateTime fromDate, DateTime toDate, string subscriptionId, string resourceGroupName, string sessionPoolName, string managedEnvironmentName)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetCustomContainerSessionActivatorLogs", region,
         new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "subscriptionId", subscriptionId },
                { "resourceGroupName", resourceGroupName },
                { "sessionPoolName", sessionPoolName },
                { "managedEnvironmentName", managedEnvironmentName }
         });
    }
}
