using Azure.Core;
using Azure.Identity;
using Microsoft.SemanticKernel;
using OperationalAgentRuntime.Helpers;
using System.ComponentModel;
using System.Net.Http.Headers;

public class RemediationPlugin
{
    [KernelFunction("scale_app_service_plan_vertically")]
    [Description("Scale up an App Service Plan to a higher tier. Useful when experiencing memory leaks. Prioritizes Premium v2/v3 tiers for better memory allocation.A scale up operation would incur a cost increase similarly a scale down operation would save costs, customer must be notified.")]
    public async Task<RemediationResult> ScaleAppServicePlanVertically(
        [Description("The resource ID of the App Service.")]
        string resourceId)
    {
        try
        {
            Console.WriteLine($"[scale_app_service_plan_vertically] Invoked with resourceId: {resourceId}");

            // Get App Service Plan ID from Web App
            var appServicePlanId = await ArmHelper.GetAppServicePlanNameAsync(resourceId);

            // Get current SKU
            var currentSku = await ArmHelper.GetCurrentSkuAsync(appServicePlanId);

            // Get next SKU in progression
            var targetSku = ArmHelper.GetNextSku(currentSku);

            // Perform scaling operation
            var success = await ArmHelper.ScaleUpAppServicePlanByNameAsync(
                appServicePlanId,
                targetSku);

            return new RemediationResult(
                Success: success,
                Action: $"Scaled App Service Plan to {targetSku.Name}",
                Details: $"Previous tier: {currentSku.Name}");
        }
        catch (Exception ex)
        {
            return new RemediationResult(
                Success: false,
                Action: "Failed to scale App Service Plan",
                Details: ex.Message);
        }
    }

    [KernelFunction("collect_memory_dump")]
    [Description("Collect memory dump from an App Service experiencing memory leaks for analysis.")]
    public async Task<RemediationResult> CollectMemoryDump(
        [Description("The resource ID of the App Service.")]
        string resourceId)
    {
        try
        {
            Console.WriteLine($"[collect_memory_dump] Invoked with resourceId: {resourceId}");
            var dumpPath = await ArmHelper.TakeMemoryDumpAsync(resourceId);

            return new RemediationResult(
                Success: !string.IsNullOrEmpty(dumpPath),
                Action: "Memory dump collected",
                Details: !string.IsNullOrEmpty(dumpPath) ?
                    $"Dump available at: {dumpPath}" :
                    "Failed to collect memory dump");
        }
        catch (Exception ex)
        {
            return new RemediationResult(
                Success: false,
                Action: "Failed to collect memory dump",
                Details: ex.Message);
        }
    }

    [KernelFunction("restart_webapp")]
    [Description("Restart a Web App instance to mitigate memory leaks. This is typically used after scaling up " +
               "if memory issues persist. The restart will clear the memory and start fresh.")]
    public async Task<RemediationResult> RestartWebApp(
       [Description("The resource ID of the Web App.")]
       string resourceId)
    {
        try
        {
            Console.WriteLine($"[restart_webapp] Invoked with resourceId: {resourceId}");

            var httpClient = new HttpClient();
            var token = await GetAccessTokenAsync();
            var requestUrl = $"https://management.azure.com{resourceId}/restart?api-version=2021-02-01";

            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await httpClient.SendAsync(request);

            return new RemediationResult(
                Success: response.IsSuccessStatusCode,
                Action: "Restarted Web App",
                Details: response.IsSuccessStatusCode ?
                    "Restart completed successfully" :
                    $"Failed to restart: {response.ReasonPhrase}");
        }
        catch (Exception ex)
        {
            return new RemediationResult(
                Success: false,
                Action: "Failed to restart Web App",
                Details: ex.Message);
        }
    }

   private static readonly Dictionary<string, decimal> HourlyRates = new(StringComparer.OrdinalIgnoreCase)
   {
       {"F1", 0}, {"D1", 0},
       {"B1", 0.074M}, {"B2", 0.149M}, {"B3", 0.298M},
       {"S1", 0.100M}, {"S2", 0.199M}, {"S3", 0.399M},
       {"Premium1v2", 0.227M}, {"Premium2v2", 0.454M}, {"Premium3v2", 0.908M},
       {"Premium0v3", 0.078M}, {"Premium1v3", 0.252M}, {"Premium2v3", 0.504M}, {"Premium3v3", 1.008M},
   };

    [KernelFunction("calculate_scaling_cost")]
    [Description("Calculates the cost difference between current and target SKUs")]
    public async Task<RemediationResult> CalculateScalingCost(
        [Description("The resource ID of the App Service.")]
       string resourceId,
       [Description("Direction of scaling - 'up' or 'down'")]
       string direction,
        [Description("Current SKU of the app service plane")]
        string currentSku,
        [Description("Possible new sku of the app service plan")]
        string targetSku)
    {
        try
        {
            Console.WriteLine($"[calculate_scaling_cost] Invoked with resourceId: {resourceId}, direction: {direction}, currentSku: {currentSku}, targetSku: {targetSku}");
            var appServicePlanId = await ArmHelper.GetAppServicePlanNameAsync(resourceId);

            if (!HourlyRates.TryGetValue(currentSku, out var currentRate))
                return new RemediationResult(false, "Cost Calculation", "Current SKU rate not found");

            if (!HourlyRates.TryGetValue(targetSku, out var targetRate))
                return new RemediationResult(false, "Cost Calculation", "Target SKU rate not found");

            var hourlyDiff = targetRate - currentRate;
            var dailyDiff = hourlyDiff * 24;
            var monthlyDiff = dailyDiff * 30;

            return new RemediationResult(
                Success: true,
                Action: $"Cost difference for scaling {direction} from {currentSku} to {targetSku}",
                Details: $"Hourly: ${hourlyDiff:F3}\nDaily: ${dailyDiff:F2}\nMonthly: ${monthlyDiff:F2}"
            );
        }
        catch (Exception ex)
        {
            return new RemediationResult(
                Success: false,
                Action: "Failed to calculate scaling costs",
                Details: ex.Message
            );
        }
    }

private async Task<string> GetAccessTokenAsync()
    {
        var credential = new DefaultAzureCredential();
        var tokenRequestContext = new TokenRequestContext(["https://management.azure.com/.default"]);
        var token = await credential.GetTokenAsync(tokenRequestContext, default);
        return token.Token;
    }
}

public sealed record RemediationResult(
    bool Success,
    string Action,
    string Details);
