// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Interface
{
    public interface ICpuAnalysisPlugin
    {
        public Guid? ThreadId { get; set; }

        Task<string> ScaleUpAppServicePlanBySku(string resourceId);
        Task<string> AutoScaleApp(string subscriptionId, string resourceGroupName, string autoScaleSettingName, string location, string resourceId, int minCount, int maxCount, int targetCount, string profileName, string metricName, string operatorProperty, double threshold, string timeAggregation, string statistic, string timeGrain, string timeWindow, string scaleDirection, string scaleType, string scaleValue, string cooldown);
        Task<bool> ShouldTriggerHighMemoryScenario(string resourceId, double spikeThreshold = 0.2, double endWindowFraction = 0.1, double sustainedDropLength = 3);
        Task<bool> ShouldTriggerHighCPUScenario(string resourceId, double spikeThreshold = 0.5, double endWindowFraction = 0.1, double sustainedDropLength = 3);
    }
}
