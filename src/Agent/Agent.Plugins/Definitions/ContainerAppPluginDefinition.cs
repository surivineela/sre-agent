// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Plugins.Models;
using Azure.ResourceManager.Network;
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

        [KernelFunction("get_containerapp_info")]
        [Description("Get detailed information about a container app including the revision history.")]
        public async Task<ContainerAppDescriptor> GetContainerAppInfoAsync(
            [Description("The resource ID of the Container App.")]string resourceId)
        {   
            return await _containerAppPlugin.GetContainerAppInfoAsync(resourceId);
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

        #region NSG Plugins 

        [KernelFunction("get_containerapp_nsg_rules")]
        [Description("Retrieves all Network Security Groups (NSGs) associated with a Container App and their security rules. " +
            "Returns a dictionary where keys are NSG resource IDs and values are lists of security rules. " +
            "Use this to identify network access issues or restrictive rules that might be blocking traffic to/from the Container App.")]
        public async Task<IDictionary<string, IReadOnlyList<SecurityRuleData>>> GetAllNSGRulesForContainerAppAsync(
            [Description("The resource ID of the Container App instance.")] string resourceId)
        {
            return await _containerAppPlugin.GetAllNSGRulesForContainerAppAsync(resourceId);
        }

        [KernelFunction("create_or_update_nsg_rule")]
        [Description("Creates a new NSG rule or updates an existing one to modify network access permissions. Use this to fix connectivity issues by allowing necessary traffic or blocking unwanted traffic.")]
        public async Task<bool> CreateOrUpdateNSGRuleAsync(
            [Description("Azure resource ID of the NSG to update")] string nsgResourceId,
            [Description("The security rule data object containing all rule configuration")] SecurityRuleData rule)
        {
            return await _containerAppPlugin.CreateOrUpdateNSGRuleAsync(nsgResourceId, rule);
        }

        [KernelFunction("remove_nsg_rule")]
        [Description("Removes an existing NSG rule. Use this to eliminate overly restrictive or unnecessary security rules.")]
        public async Task<bool> RemoveNSGRuleAsync(
            [Description("Azure resource ID of the NSG containing the rule")] string nsgResourceId,
            [Description("Name of the security rule to remove")] string ruleName)
        {
            return await _containerAppPlugin.RemoveNSGRuleAsync(nsgResourceId, ruleName);
        }

        #endregion

        [KernelFunction("scale_container_app")]
        [Description("Scales a Container App by adjusting its memory allocation and replica count. Use this to resolve performance or availability issues by increasing resources or scaling out the application.")]
        public async Task<bool> ScaleContainerApp(
            [Description("Azure resource ID of the Container App to scale")] string resourceId,
            [Description("Desired memory allocation (e.g., '1Gi', '512Mi')")] string desiredMemory,
            [Description("Minimum number of replicas to run (e.g., 1)")] int minReplicas,
            [Description("Maximum number of replicas to scale to (e.g., 10)")] int maxReplicas)
        {
            if (string.IsNullOrWhiteSpace(resourceId) || string.IsNullOrWhiteSpace(desiredMemory) || minReplicas < 0 || maxReplicas < minReplicas)
            {
                throw new ArgumentException("Invalid input parameters.");
            }

            return await _containerAppPlugin.ScaleContainerApp(resourceId, desiredMemory, minReplicas, maxReplicas);
        }
    }
}