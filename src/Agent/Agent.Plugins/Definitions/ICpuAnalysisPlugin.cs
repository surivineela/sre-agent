// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;

namespace Agent.Plugins
{
    public interface ICpuAnalysisPlugin
    {
        public Guid? ThreadId { get; set; }

        Task<string> ScaleUpAppServicePlanBySku(string resourceId);
        Task<string> CollectMemoryDumpForApp(string resourceId);
        Task<string> CollectProfileForApp(string resourceId, int durationOfTraceInSeconds);
        Task<string> AutoScaleApp(string subscriptionId, string resourceGroupName, string autoScaleSettingName, string location, string resourceId, int minCount, int maxCount, int targetCount, string profileName, string metricName, string operatorProperty, double threshold, string timeAggregation, string statistic, string timeGrain, string timeWindow, string scaleDirection, string scaleType, string scaleValue, string cooldown);
    }
}
