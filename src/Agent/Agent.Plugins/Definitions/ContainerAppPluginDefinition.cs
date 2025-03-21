// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Plugins.Models;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Definitions
{
    public class ContainerAppPluginDefinition
    {
        private readonly IContainerAppPlugin _containerAppPlugin;

        public ContainerAppPluginDefinition(IContainerAppPlugin containerAppPlugin)
        {
            _containerAppPlugin = containerAppPlugin;
        }

        [KernelFunction("get_latest_containerapp_revision")]
        [Description("Get the latest active revision for a Container App instance")]
        public async Task<RevisionInfo?> GetLatestRevisionAsync(string resourceId)
        {
            return await _containerAppPlugin.GetLatestRevisionAsync(resourceId);
        }

        [KernelFunction("list_container_apps")]
        public async Task<IReadOnlyList<ContainerAppDescriptor>> ListContainerAppsAsync(
            [Description("The subscription ID to scan for Container Apps.")] Guid subscriptionId)
        {
            return await _containerAppPlugin.ListContainerAppsAsync(subscriptionId);
        }

        [KernelFunction("restart_containerapp_revision")]
        public async Task<string> RestartContainerApp(
            [Description("The resource ID of the Container App.")]
            string appResourceId,
            [Description("Container App revision name to restart.")]
            string revisionName)
        {
            return await _containerAppPlugin.RestartContainerApp(appResourceId, revisionName);
        }

        #region Metrics

        [KernelFunction("get_containerapp_request_count_metrics")]
        [Description("Start a background operation to get the total request count metrics of a specific Container App instance at per minute granularity" +
        " for the past 30 minutes, Container App is healthy if all data points are at least 99.9 availability.")]
        public async Task<IReadOnlyList<RequestCountTimeSeriesData>> GetContainerAppRequestMetrics(
      [Description("The resource ID of the ContainerApp resource.")] string resourceId)
        {
            return await _containerAppPlugin.GetContainerAppRequestMetrics(resourceId);
        }

        [KernelFunction("get_containerapp_memory_metrics")]
        [Description("Start a background operation to get the average memory usage of a specific Container App instance at per minute granularity for the past 30 minutes," +
        " Container App is healthy if over half of the data points is less than 80% memory utilization.")]
        public async Task<IReadOnlyList<MemoryUsageTimeSeriesData>> GetContainerAppMemoryMetrics(
            [Description("The resource ID of the ContainerApp resource.")] string resourceId)
        {
            return await _containerAppPlugin.GetContainerAppMemoryMetrics(resourceId);
        }

        [KernelFunction("get_containerapp_cpu_metrics")]
        [Description("Get the average CPU utilization metrics of a specific Container App instance at per minute granularity" +
                 " for the past 30 minutes, Container App is healthy if over half of the data points is less than 80% CPU utilization, zero metric value doesn't indicate the app is unhealthy")]
        public async Task<IReadOnlyList<CpuUsageTimeSeriesData>> GetContainerAppCpuMetrics(
            [Description("The resource ID of the ContainerApp resource.")] string resourceId)
        {
            return await _containerAppPlugin.GetContainerAppCpuMetrics(resourceId);
        }

        #endregion
    }
}
