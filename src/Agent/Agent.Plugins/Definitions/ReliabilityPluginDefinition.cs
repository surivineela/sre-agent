using Agent.Core.Attributes;
using Agent.Plugins.Attributes;
using Agent.Plugins.Interface;
using Azure.ResourceManager.AppService.Models;
using Microsoft.DurableTask.Client;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace Agent.Plugins.Definitions
{
    public class ReliabilityPluginDefinition
    {
        private readonly IReliabilityPlugin _reliabilityPlugin;
        public ReliabilityPluginDefinition(IReliabilityPlugin reliabilityPlugin)
        {
            _reliabilityPlugin = reliabilityPlugin;
        }

        [KernelFunction("update_alwaysOn")]
        [Description("To modify the AlwaysOn property of the app service")]
        [RequiresApproval]
        [WriteAction]
        public async Task<string> UpdateAlwaysOn(
            [Description("The resource ID of the app service resource to modify.")]
            string resourceId,
            [Description("The new boolean value of AlwaysOn that the app service will be updated to have")]
            bool enabled = true)
        {
            return await _reliabilityPlugin.UpdateAlwaysOn(resourceId, enabled);
        }

        [Description("To modify the AutoHeal properties of the app service")]
        [RequiresApproval]
        [WriteAction]
        public async Task<string> UpdateAutoHeal(
            [Description("The resource ID of the app service resource to modify.")]
            string resourceId,
            [Description("The boolean AutoHeal value that the app service will be updated to have")]
            bool autoHealEnabled,
            [Description("The autoheal configuration that the app service will be updated to have")]
            AutoHealRules autoHealRules)
        {
            return await _reliabilityPlugin.UpdateAutoHeal(resourceId, autoHealEnabled, autoHealRules);
        }

        [KernelFunction("update_health_check_path")]
        [Description("To modify the Healthcheck property of the app service")]
        public async Task<string> UpdateHealthCheck(
            [Description("The resource ID of the app service resource to modify.")]
            string resourceId,
            [Description("The healthCheckPath that the app service wil be updated to have")]
            string healthCheckPath = "/health")
        {
            return await _reliabilityPlugin.UpdateHealthCheck(resourceId, healthCheckPath);
        }

        [KernelFunction("update_number_of_workers")]
        [Description("To change the number of workers that the app service is hosted on")]
        [RequiresApproval]
        [WriteAction]
        public async Task<string> UpdateHostWorkers(
            [Description("The resource ID of the app service resource to modify.")]
            string resourceId,
            [Description("The new number of workers that the app service resource will be hosted on.")]
            int numberOfWorkers = 3)
        {
            return await _reliabilityPlugin.UpdateHostWorkers(resourceId, numberOfWorkers);
        }

        [KernelFunction("get_app_service_reliability")]
        [Description("To find how resilient, reliable, and optimal an app service is.")]
        public async Task<string> GetReliabilityStatus(
            [Description("The resource ID of the app service resource.")] string resourceId
        )
        {
            return await _reliabilityPlugin.GetReliabilityStatus(resourceId);
        }

        [KernelFunction("get_subscription_reliability")]
        [Description("To find how resilient, reliable, and optimal the app services are under a subscription.")]
        public async Task<string> GetReliabilityStatusForSubscriptions(CancellationToken cancellationToken = default)
        {
            return await _reliabilityPlugin.GetReliabilityStatusForSubscriptions(cancellationToken);
        }

        [Submit202(ExecuteMethodName = nameof(GetReliabilityOrchestrationStatus))]
        [KernelFunction("get_apps_to_monitor")]
        [Description("To find the apps and their metrics relevant to resilience, reliability, and optimization")]
        public async Task<string> GetAppsToMonitor(CancellationToken cancellationToken = default)
        {
            return await _reliabilityPlugin.GetAppsToMonitor(cancellationToken);
        }

        [KernelFunction("get_reliability_orchestration_status")]
        [Description("To get the current state or status of the orchestration task to update the apps' reliability metrics")]
        public async Task<OrchestrationRuntimeStatus?> GetReliabilityOrchestrationStatus(string instanceId)
        {
            return await _reliabilityPlugin.GetReliabilityOrchestrationStatus(instanceId);
        }
    }
}

