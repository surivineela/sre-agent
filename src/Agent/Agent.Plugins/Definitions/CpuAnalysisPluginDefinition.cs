// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Attributes;
using Agent.Framework;
using Agent.Plugins.Interface;
using Microsoft.SemanticKernel;

namespace Agent.Plugins
{
    [AgentToolPlugin]
    public class CpuAnalysisPluginDefinition
    {
        private readonly ICpuAnalysisPlugin _cpuAnalysisPlugin;

        public CpuAnalysisPluginDefinition(ICpuAnalysisPlugin cpuAnalysisPlugin)
        {
            _cpuAnalysisPlugin = cpuAnalysisPlugin;
        }

        [KernelFunction("scale_up_app_service_plan_by_sku")]
        [Description("Scale up the app service plan by sku")]
        [RequiresApproval]
        public async Task<string> ScaleUpAppServicePlanBySku(
        [Description("resourceId of the app")] string resourceId)
        {
            return await _cpuAnalysisPlugin.ScaleUpAppServicePlanBySku(resourceId);
        }

        [KernelFunction("autoscale_app_service")]
        [Description("Create AutoScale Settings for App to Autoscale App")]
        [RequiresApproval]
        public async Task<string> AutoScaleApp(
            [Description("resourceId of the app")] string subscriptionId,
            string resourceGroupName,
            string autoScaleSettingName,
            string location,
            string resourceId,
            int minCount,
            int maxCount,
            int targetCount,
            string profileName = "DefaultProfile",
            string metricName = "CpuPercentage",
            string operatorProperty = "GreaterThan",
            double threshold = 70.0,
            string timeAggregation = "Average",
            string statistic = "Average",
            string timeGrain = "PT1M",
            string timeWindow = "PT5M",
            string scaleDirection = "Increase",
            string scaleType = "ChangeCount",
            string scaleValue = "1",
            string cooldown = "PT5M")
        {

            return await _cpuAnalysisPlugin.AutoScaleApp(
                subscriptionId,
                resourceGroupName,
                autoScaleSettingName,
                location,
                resourceId,
                minCount,
                maxCount,
                targetCount,
                profileName,
                metricName,
                operatorProperty,
                threshold,
                timeAggregation,
                statistic,
                timeGrain,
                timeWindow,
                scaleDirection,
                scaleType,
                scaleValue,
                cooldown);
        }

        [Description("Check if the high memory scenario should be triggered based on a spike of the memory.")]
        public async Task<bool> ShouldTriggerHighMemoryScenario(
            [Description("The full Azure resource ID of the resource to diagnose (format: /subscriptions/{subId}/resourceGroups/{rgName}/providers/{resourceProvider}/{resourcetype}/{resourceName}).")] string resourceId)
        {
            return await _cpuAnalysisPlugin.ShouldTriggerHighMemoryScenario(resourceId);
        }

        [Description("Check if the high cpu scenario should be triggered based on a spike of the cpu.")]
        public async Task<bool> ShouldTriggerHighCPUScenario(
            [Description("The full Azure resource ID of the resource to diagnose (format: /subscriptions/{subId}/resourceGroups/{rgName}/providers/{resourceProvider}/{resourcetype}/{resourceName}).")] string resourceId)
        {
            return await _cpuAnalysisPlugin.ShouldTriggerHighCPUScenario(resourceId);
        }
    }
}
