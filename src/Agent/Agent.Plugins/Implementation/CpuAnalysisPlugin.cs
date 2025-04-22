using System;
using System.Collections.Generic;
using System.ComponentModel;
using Agent.Core.Helpers;
using Microsoft.SemanticKernel;
using Newtonsoft.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Identity;

namespace Agent.Plugins;

public class CpuAnalysisPlugin : ICpuAnalysisPlugin
{
    private readonly ArmHelper _armHelper;
    public Guid? ThreadId { get; set; }


    public CpuAnalysisPlugin(ArmHelper armHelper)
    {
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

    [KernelFunction("collect_memory_dump_for_app")]
    [Description("Collect Memory Dump for App")]
    public async Task<string> CollectMemoryDumpForApp(
    [Description("resourceId of the app")] string resourceId)
    {
        // call arm helper
        var responseString = await _armHelper.TakeMemoryDumpAsync(resourceId);

        if (String.IsNullOrEmpty(responseString))
        {
            throw new Exception($"There was an issue collecting the memory dump for {resourceId}");
        }
        var memoryDump = await ReadMemoryDumpFromStorageAsync("dummyUrl", "dummyContainer", "dummyBlob");
        return $"The memory dump for {resourceId} has been collected:\n{memoryDump}";
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
}

