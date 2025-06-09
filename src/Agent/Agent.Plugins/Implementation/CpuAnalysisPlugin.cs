// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Helpers;
using Microsoft.SemanticKernel;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Identity;
using Agent.Framework;

namespace Agent.Plugins;

public class CpuAnalysisPlugin : ICpuAnalysisPlugin
{
    private readonly ArmHelper _armHelper;
    private readonly IMetricsPlugin _metricsPlugin;

    public Guid? ThreadId { get; set; }

    public CpuAnalysisPlugin(ArmHelper armHelper, IMetricsPlugin metricsPlugin)
    {
        _metricsPlugin = metricsPlugin;
        _armHelper = armHelper;
    }

    [KernelFunction("scale_up_app_service_plan_by_sku")]
    [Description("Scale up the app service plan by sku")]
    public async Task<string> ScaleUpAppServicePlanBySku(
    [Description("resourceId of the app")] string resourceId)
    {
        var appServicePlanId = await _armHelper.GetAppServicePlanNameAsync(resourceId);
        var currentSku = await _armHelper.GetCurrentSkuAsync(appServicePlanId);
        var nextSku = ArmHelper.GetNextSku(currentSku);

        var success = await _armHelper.ScaleUpAppServicePlanByNameAsync(resourceId, nextSku);
        if (success)
        {
            return $"The app service plan for {resourceId} has been scaled up to {nextSku.Name}";
        }
        return $"There was an issue scaling up your app service plan";
    }

    [KernelFunction("autoscale_app_service")]
    [Description("Create AutoScale Settings for App to Autoscale App")]
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
        var response = await _armHelper.CreateAutoScaleSetting(
             subscriptionId,
             resourceGroupName,
             autoScaleSettingName,
             location,
             resourceId,
             minCount,
             maxCount,
             targetCount, // Argument 8: Changed from string to int
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
             cooldown
         );


        if (String.IsNullOrEmpty(response))
        {
            return "There was an issue creating the auto-scaling configuration.";
        }

        return "Auto-scaling configuration has been successfully applied. ";
    }

    private async Task<string> ReadMemoryDumpFromStorageAsync(string accountUrl, string containerName, string blobName)
    {
        var credential = new DefaultAzureCredential();
        var blobServiceClient = new BlobServiceClient(new Uri(accountUrl), credential);
        BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

        BlobClient blobClient = containerClient.GetBlobClient(blobName);

        BlobDownloadInfo resultInfo = await blobClient.DownloadAsync();
        var buffer = new byte[resultInfo.ContentLength];
        StreamReader reader = new StreamReader(resultInfo.Content);
        string res = reader.ReadToEnd();
        return res;
    }

    private async Task<bool> ShouldTriggerDiagnosticScenario(List<double> values, double spikeThreshold = 0.2, double endWindowFraction = 0.3, double sustainedDropLength = 3)
    {
        bool HasRecentSpike(List<double> values)
        {
            int windowSize = (int)(values.Count * endWindowFraction);
            windowSize = Math.Max(windowSize, 3);

            if (values.Count < windowSize + 1)
                return false;

            double baseline = values[values.Count - windowSize - 1];

            for (int i = values.Count - windowSize; i < values.Count; i++)
            {
                double change = (values[i] - baseline) / baseline;
                if (change >= spikeThreshold)
                    return true;
            }

            return false;
        }

        int dropStartIndex = FindSustainedDropStartIndex(values);

        // Slice the original time series data, not just the values
        var dataToEvaluate = dropStartIndex >= 0
            ? values.ToList().GetRange(dropStartIndex, values.Count - dropStartIndex)
            : values;

        bool IsMonotonicallyIncreasing(List<double> values)
        {
            int increaseCount = 0;
            for (int i = 1; i < values.Count; i++)
            {
                if (values[i] > values[i - 1])
                    increaseCount++;
            }

            double increaseRatio = (double)increaseCount / (values.Count - 1);
            return increaseRatio > 0.8;
        }

        int FindSustainedDropStartIndex(List<double> values)
        {
            int dropCount = 0;

            for (int i = values.Count - 1; i > 0; i--)
            {
                if (values[i] < values[i - 1])
                    dropCount++;
                else
                    dropCount = 0;

                if (dropCount >= sustainedDropLength)
                    return i;
            }

            return -1;
        }

        if (IsMonotonicallyIncreasing(dataToEvaluate))
            return true;

        if (HasRecentSpike(dataToEvaluate))
            return true;

        return false;
    }

    public async Task<bool> ShouldTriggerHighMemoryScenario(string resourceId, double spikeThreshold = 0.2, double endWindowFraction = 0.1, double sustainedDropLength = 3)
    {
        var memorySeries = await _metricsPlugin.GetMemoryMetrics(resourceId);
        if (memorySeries == null || memorySeries.Count < 5)
            return false;

        return await ShouldTriggerDiagnosticScenario(memorySeries.Select(m => m.AverageMemoryInBytes).ToList(), spikeThreshold, endWindowFraction, sustainedDropLength);
    }

    public async Task<bool> ShouldTriggerHighCPUScenario(string resourceId, double spikeThreshold = 0.2, double endWindowFraction = 0.1, double sustainedDropLength = 3)
    {
        var cpuSeries = await _metricsPlugin.GetWebAppCpuMetrics(resourceId);
        double averageCpuMetric = cpuSeries.Average(cpu => cpu.AverageCpuUtilizationPercentage);
        if (cpuSeries == null || cpuSeries.Count < 5)
            return false;

        return averageCpuMetric > 50 && await ShouldTriggerDiagnosticScenario(cpuSeries.Select(m => m.AverageCpuUtilizationPercentage).ToList(), spikeThreshold, endWindowFraction, sustainedDropLength);
    }
}

