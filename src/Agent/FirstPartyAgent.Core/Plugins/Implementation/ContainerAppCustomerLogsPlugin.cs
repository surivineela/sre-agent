// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Plugins;
using Microsoft.Diagnostics.Runtime.AbstractDac;

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

    public Task<string> GetAppsAndjobsVolumeForEnv(string region, DateTime fromDate, DateTime toDate, string managedClusterName)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetAppsAndjobsVolumeForEnv", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "managedClusterName", managedClusterName }
        });
    }

    public Task<string> GetEventProcessorPods(string region, DateTime fromDate, DateTime toDate, string managedClusterName)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetPodsWithPrefix", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "managedClusterName", managedClusterName },
            { "podNamePrefix", "k8se-event-processor" }
        });
    }

    public Task<string> GetLogProcessorPods(string region, DateTime fromDate, DateTime toDate, string managedClusterName)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetPodsWithPrefix", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "managedClusterName", managedClusterName },
            { "podNamePrefix", "k8se-log-processor" }
        });
    }

    public Task<string> GetEventProcessorPodStatus(string region, DateTime fromDate, DateTime toDate, string managedClusterName)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetPodHealthStatus", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "managedClusterName", managedClusterName },
            { "podNamePrefix", "k8se-event-processor" },
            { "podNamespace", "k8se-system" }
        });
    }

    public Task<string> GetLogProcessorPodStatus(string region, DateTime fromDate, DateTime toDate, string managedClusterName)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetPodHealthStatus", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "managedClusterName", managedClusterName },
            { "podNamePrefix", "k8se-log-processor" },
            { "podNamespace", "k8se-system" }
        });
    }

    public Task<string> GetContainerAppWorkloadProfile(string region, DateTime fromDate, DateTime toDate, string containerAppOrJobName)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetContainerAppWorkloadProfile", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "containerAppOrJobName", containerAppOrJobName }
        });
    }

    public Task<string> GetFluentbitOutputErrors(string region, DateTime fromDate, DateTime toDate, string managedClusterName)
    {
        return _kustoPlugin.ExecuteLocalFunctionAsync("GetFluentbitOutputErrorsForApp", region,
        new Dictionary<string, string> {
            { "region", region.ToString() },
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "managedClusterName", managedClusterName },
            { "metricName", "fluentbit_output_errors_total" }
        });
    }
}
