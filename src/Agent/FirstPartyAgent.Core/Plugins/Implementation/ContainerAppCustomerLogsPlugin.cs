// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Plugins;

namespace FirstPartyAgent.Core.Plugins.Implementation;

public class ContainerAppCustomerLogsPlugin : IContainerAppCustomerLogsPlugin
{
    private readonly IKustoPluginChat _kustoPlugin;

    public ContainerAppCustomerLogsPlugin(IKustoPluginChat kustoPlugin)
    {
        _kustoPlugin = kustoPlugin;
    }

    public Task<string> GetLogConfiguration(string region, DateTime fromDate, DateTime toDate, Guid customerSubscriptionId, string managedEnvironmentName, string managedClusterName)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetCustomerLogConfiguration", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "customerSubscriptionId", customerSubscriptionId.ToString() },
            { "managedEnvironmentName", managedEnvironmentName },
            { "managedClusterName", managedClusterName }
        });
    }

    public Task<string> GetEventProcessorErrors(string region, DateTime fromDate, DateTime toDate, string managedClusterName, string containerAppOrJobName)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetEventProcessorErrors", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "managedClusterName", managedClusterName },
            { "containerAppOrJobName", containerAppOrJobName }
        });
    }

    public Task<string> GetEventProcessorLeaderElectionEvents(string region, DateTime fromDate, DateTime toDate, string managedClusterName)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetEventProcessorLeaderElectionEvents", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "managedClusterName", managedClusterName }
        });
    }
}
