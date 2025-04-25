// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Helpers;
using Microsoft.SemanticKernel;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Identity;
using Azure.Core;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager;
using System.Net.Http.Headers;

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

        string memoryDumpFile = Path.GetTempFileName() + ".dmp";

        // Write dump file content to a temp file.
        try
        {
            try
            {
                try
                {
                    using HttpClient client = new();
                    using (var fileStream = new FileStream(memoryDumpFile, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var responseStream = await client.GetAsync(responseString);
                        await responseStream.Content.CopyToAsync(fileStream);
                    }
                }
                catch (HttpRequestException ex)
                {
                    throw new Exception($"Failed to download memory dump for {resourceId}: {ex.Message}");
                }
                catch (IOException ex)
                {
                    throw new Exception($"Failed to write memory dump to file for {resourceId}: {ex.Message}");
                }
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Failed to download memory dump for {resourceId}: {ex.Message}");
            }
            catch (IOException ex)
            {
                throw new Exception($"Failed to write memory dump to file for {resourceId}: {ex.Message}");
            }
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Failed to download memory dump for {resourceId}: {ex.Message}");
        }
        catch (IOException ex)
        {
            throw new Exception($"Failed to write memory dump to file for {resourceId}: {ex.Message}");
        }

        return $"The memory dump for {resourceId} has been collected:\n{memoryDumpFile}";
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

    [KernelFunction("collect_profile_for_app")]
    [Description("Collect a profile or trace for an App Service to assess CPU activity.")] 
    public async Task<string> CollectProfileForApp(string resourceId, int durationOfTraceInSeconds = 20)
    {
        // TODO: Use the HttpClient factory?
        using HttpClient client = new();

        // Get the Kudu URL.
        var credential = new DefaultAzureCredential();
        var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
        var token = await credential!.GetTokenAsync(tokenRequestContext, default);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        var armClient = new ArmClient(credential);
        ResourceIdentifier resourceIdentifier = new ResourceIdentifier(resourceId);
        WebSiteResource webApp = await armClient.GetWebSiteResource(resourceIdentifier).GetAsync();
        var appData = webApp.Data;
        string scmHostName = appData.EnabledHostNames.FirstOrDefault(h => h.Contains(".scm."));
        if (appData?.IsScmSiteAlsoStopped == false)
        {
            throw new ArgumentException("The Kudu site is not running. Please start the Kudu site to collect the profile.");
        }

        string kuduUrl = $"https://{scmHostName}/api";

        // Get the processes and for Linux find the default process running the app.
        string processesUrl = $"{kuduUrl}/processes";
        var processesResponse = await client.GetAsync(processesUrl);
        string response = await processesResponse.Content.ReadAsStringAsync();
        List<Dictionary<string, object>> processes = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(response);
        Dictionary<string, object> defaultProcess = processes?.FirstOrDefault(p => p["isDefault"]?.ToString()?.ToLower() == "true");

        // TODO: Adjust this for the Windows .diagsession.
        string profileLink = $"{kuduUrl}/processes/0/profile/start?durationSeconds={durationOfTraceInSeconds}";
        if (defaultProcess is not null)
        {
            profileLink = $"{kuduUrl}/processes/{defaultProcess["pid"]}/profile/start?durationSeconds={durationOfTraceInSeconds}";
        }

        // Starts a request to the Kudu API to take a profile of the default process based on the pid.
        // TODO: Figure out the path for the Windows call.
try
{
    var profileResponse = await client.GetAsync(profileLink);
    string tempFilePath = Path.GetTempFileName() + ".nettrace";

    using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
    {
        var responseStream = await profileResponse.Content.ReadAsStreamAsync();
        await responseStream.CopyToAsync(fileStream);
    }

    return $"The profile for {resourceId} has been collected and saved to: {tempFilePath}";
}
catch (HttpRequestException ex)
{
    return $"An error occurred while collecting the profile: {ex.Message}";
}
    }
}

