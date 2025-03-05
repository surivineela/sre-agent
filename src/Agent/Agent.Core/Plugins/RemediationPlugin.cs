// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Azure;
using Azure.Core;
using Azure.Identity;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Net.Http.Headers;
using Agent.Core.Models;
using Agent.Core.Helpers;

namespace Agent.Core.Plugins;

public class RemediationPlugin

{
    public static RemediationResult? IsOperationNotApproved(
        string operationName,
        string resourceId,
        out ApprovalStatus? approvalStatus,
        string approvalBaseLink = null)
    {
        approvalStatus = null;
        if (string.IsNullOrEmpty(approvalBaseLink))
        {
            // Use the default approval link
            approvalBaseLink = "https://approval-app-affhfqdfcfc8gkgq.westus-01.azurewebsites.net";
        }

        var approvalDescriptor = new ApprovalDescriptor(
            resourceId,
            operationName);

        if (!GlobalStatic.ApprovalStatus.TryGetValue(approvalDescriptor, out approvalStatus))
        {
            // If approvalBaseLink is provided, try to check remote approval status
            if (!string.IsNullOrEmpty(approvalBaseLink))
            {
                try
                {
                    var remoteApprovalTask = ApprovalHelper.PullApprovalResult(approvalBaseLink, $"{operationName}_{resourceId.GetHashCode()}");
                    var remoteApproval = remoteApprovalTask.GetAwaiter().GetResult();

                    if (remoteApproval != null)
                    {
                        // Create a new approval status from the remote result while respecting the existing structure
                        approvalStatus = new ApprovalStatus(
                            OperationId: remoteApproval.Id,
                            StartTime: DateTime.Now.AddMinutes(-5), // Assuming approval started a bit earlier
                            ApprovedTime: remoteApproval.IsApproved ? DateTime.Now : null,
                            DecisionMaker: remoteApproval.IsApproved ? remoteApproval.ApproverName : null,
                            ProcessedTime: null,
                            description: $"Remote approval for operation {operationName} on resource {resourceId}"
                        );

                        // Update the cache
                        GlobalStatic.ApprovalStatus[approvalDescriptor] = approvalStatus;

                        // If approved, return null to proceed with operation
                        if (approvalStatus.IsApproved)
                        {
                            return null;
                        }

                        // If not approved, return the not approved message
                        return new RemediationResult(
                            Success: false,
                            Action: "No action taken because this operation is not approved yet",
                            Details: "Ask the user to approve the operation by going through the approval process",
                            OperationId: approvalStatus.OperationId,
                            FinishedTime: DateTime.Now);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error checking remote approval status: {ex.Message}");
                }
            }

            // If no remote approval was found or checked
            return new RemediationResult(
                Success: false,
                Action: "No action taken because approval process hasn't started",
                Details: $"Approval process should be started for operation: {operationName}",
                OperationId: null,
                FinishedTime: DateTime.Now);
        }
        else if (!approvalStatus.IsApproved)
        {
            return new RemediationResult(
                Success: false,
                Action: "No action taken because this operation is not approved yet",
                Details: "Ask the user to approve the operation by going through the approval process",
                OperationId: approvalStatus.OperationId,
                FinishedTime: DateTime.Now);
        }
        return null;
    }

    [KernelFunction("scale_app_service_plan_vertically")]
    [Description("Scale up an App Service Plan to a higher tier. SHOULD be always suggested when experiencing memory leaks. Prioritizes Premium v2/v3 tiers for better memory allocation.A scale up operation would incur a cost increase similarly a scale down operation would save costs, customer must be notified.")]
    public async Task<RemediationResult> ScaleAppServicePlanVertically(
        [Description("The resource ID of the App Service.")]
        string resourceId)
    {
        var notApprovedResult = IsOperationNotApproved(
            operationName: "scale_app_service_plan_vertically",
            resourceId: resourceId,
            out var approvalStatus);
        if (notApprovedResult is not null)
        {
            return notApprovedResult;
        }

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
                Details: $"Previous tier: {currentSku.Name}",
                OperationId: approvalStatus.OperationId,
                FinishedTime: DateTime.Now);
        }
        catch (Exception ex)
        {
            return new RemediationResult(
                Success: false,
                Action: "Failed to scale App Service Plan",
                Details: ex.Message,
                OperationId: approvalStatus.OperationId,
                FinishedTime: DateTime.Now);
        }
    }

    // This is now handled by the basic auth flow

    //[KernelFunction("disable_basic_auth")]
    //[Description("Disabled basic authentication for an App Service")]
    //public async Task<RemediationResult> DisableBasicAuth(
    //    [Description("The resource ID of the App Service.")]
    //    string resourceId)
    //{
    //    var notApprovedResult = IsOperationNotApproved(operationName: "disable_basic_auth", resourceId: resourceId, out var approvalStatus);
    //    if (notApprovedResult is not null)
    //    {
    //        return notApprovedResult;
    //    }

    //    Console.WriteLine($"[disable_basic_auth] Invoked with resourceId: {resourceId}");

    //    // Authenticate using DefaultAzureCredential
    //    var credential = new DefaultAzureCredential();
    //    // Create an instance of the ArmClient to interact with Azure
    //    var armClient = new ArmClient(credential);

    //    // Get appservice using resourceId
    //    var armResourceId = new ResourceIdentifier(resourceId);
    //    var groupid = ResourceGroupResource.CreateResourceIdentifier(armResourceId.SubscriptionId, armResourceId.ResourceGroupName);

    //    try
    //    {
    //        var group = armClient.GetResourceGroupResource(groupid);
    //        var siteResponse = await group.GetWebSiteAsync(armResourceId.Name);

    //        // check if basic auth is enabled
    //        var basicAuthPublishingPolicy = siteResponse.Value.GetScmSiteBasicPublishingCredentialsPolicy();
    //        var result = await basicAuthPublishingPolicy.CreateOrUpdateAsync(WaitUntil.Completed, new CsmPublishingCredentialsPoliciesEntityData() { Allow = false });

    //        return new RemediationResult(
    //            Success: true,
    //            Action: "Basic auth disabled",
    //            Details: "none",
    //            OperationId: approvalStatus.OperationId,
    //            FinishedTime: DateTime.Now);
    //    }
    //    catch (RequestFailedException ex)
    //    {
    //        Console.Error.WriteLine($"Error in GetSqlConnectionTypeAsync: {ex.Message}");
    //        throw;
    //    }
    //}

    [KernelFunction("collect_memory_dump")]
    [Description("Collect memory dump from an App Service experiencing memory leaks for analysis.")]
    public async Task<RemediationResult> CollectMemoryDump(
        [Description("The resource ID of the App Service.")]
        string resourceId)
    {
        var notApprovedResult = IsOperationNotApproved(operationName: "collect_memory_dump", resourceId: resourceId, out var approvalStatus);
        if (notApprovedResult is not null)
        {
            return notApprovedResult;
        }

        try
        {
            Console.WriteLine($"[collect_memory_dump] Invoked with resourceId: {resourceId}");
            var dumpPath = await ArmHelper.TakeMemoryDumpAsync(resourceId);

            return new RemediationResult(
                Success: !string.IsNullOrEmpty(dumpPath),
                Action: "Memory dump collected",
                Details: !string.IsNullOrEmpty(dumpPath) ?
                    $"Dump available at: {dumpPath}" :
                    "Failed to collect memory dump",
                OperationId: approvalStatus.OperationId,
                FinishedTime: DateTime.Now);
        }
        catch (Exception ex)
        {
            return new RemediationResult(
                Success: false,
                Action: "Failed to collect memory dump",
                Details: ex.Message,
                OperationId: approvalStatus.OperationId,
                FinishedTime: DateTime.Now);
        }
    }

    [KernelFunction("restart_webapp")]
    [Description("Restart a Web App instance to mitigate memory leaks. This is typically used after scaling up " +
               "if memory issues persist. The restart will clear the memory and start fresh.")]
    public async Task<RemediationResult> RestartWebApp(
       [Description("The resource ID of the Web App.")]
       string resourceId)
    {
        var notApprovedResult = IsOperationNotApproved(operationName: "restart_webapp", resourceId: resourceId, out var approvalStatus);
        if (notApprovedResult is not null)
        {
            return notApprovedResult;
        }

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
                    $"Failed to restart: {response.ReasonPhrase}",
                OperationId: approvalStatus.OperationId,
                FinishedTime: DateTime.Now);
        }
        catch (Exception ex)
        {
            return new RemediationResult(
                Success: false,
                Action: "Failed to restart Web App",
                Details: ex.Message,
                OperationId: approvalStatus.OperationId,
                FinishedTime: DateTime.Now);
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

    [KernelFunction("possible_next_sku")]
    [Description("Given a current sku suggest a possible next sku")]
    public async Task<RemediationResult> SuggestNextSku(
    [Description("The resource ID of the App Service.")]
    string resourceId,
    [Description("Direction of scaling - 'up' or 'down'")]
    string direction,
    [Description("Current SKU of the app service plan")]
    string currentSku)
    {
        try
        {
            Console.WriteLine($"[possible_next_sku] Invoked with resourceId: {resourceId}, direction: {direction}, currentSku: {currentSku}");
            var appServicePlanId = await ArmHelper.GetAppServicePlanNameAsync(resourceId);

            // Validate current SKU exists
            if (!HourlyRates.TryGetValue(currentSku, out var currentRate))
            {
                return new RemediationResult(
                    false,
                    "SKU Suggestion",
                    $"Current SKU {currentSku} not found in rate table",
                    OperationId: null,
                    FinishedTime: DateTime.Now);
            }

            // Get the family prefix of the current SKU
            string family = GetSkuFamily(currentSku);

            // Get all SKUs in the same family
            var familySkus = HourlyRates
                .Where(kvp => GetSkuFamily(kvp.Key) == family)
                .OrderBy(kvp => kvp.Value)
                .ToList();

            // Find current SKU position
            int currentIndex = familySkus.FindIndex(kvp => kvp.Key.Equals(currentSku, StringComparison.OrdinalIgnoreCase));

            // Determine next SKU based on direction
            KeyValuePair<string, decimal>? nextSku = null;
            if (direction.Equals("up", StringComparison.OrdinalIgnoreCase) && currentIndex < familySkus.Count - 1)
            {
                nextSku = familySkus[currentIndex + 1];
            }
            else if (direction.Equals("down", StringComparison.OrdinalIgnoreCase) && currentIndex > 0)
            {
                nextSku = familySkus[currentIndex - 1];
            }

            if (nextSku == null)
            {
                return new RemediationResult(
                    false,
                    "SKU Suggestion",
                    $"No {direction} scaling option available for SKU {currentSku} in family {family}",
                    OperationId: null,
                    FinishedTime: DateTime.Now);
            }

            // Calculate cost differences
            var hourlyDiff = nextSku.Value.Value - currentRate;
            var dailyDiff = hourlyDiff * 24;
            var monthlyDiff = dailyDiff * 30;

            return new RemediationResult(
                Success: true,
                Action: $"Suggested SKU for scaling {direction} from {currentSku}",
                Details: $"Suggested SKU: {nextSku.Value.Key}\n" +
                        $"Cost difference:\n" +
                        $"Hourly: ${hourlyDiff:F3}\n" +
                        $"Daily: ${dailyDiff:F2}\n" +
                        $"Monthly: ${monthlyDiff:F2}",
                OperationId: null,
                FinishedTime: DateTime.Now);
        }
        catch (Exception ex)
        {
            return new RemediationResult(
                Success: false,
                Action: "Failed to suggest next SKU",
                Details: ex.Message,
                OperationId: null,
                FinishedTime: DateTime.Now);
        }
    }

    private static string GetSkuFamily(string sku)
    {
        if (string.IsNullOrEmpty(sku)) return string.Empty;

        // Handle free and shared tiers
        if (sku.StartsWith("F") || sku.StartsWith("D"))
            return sku[0].ToString();

        // Handle Basic tier
        if (sku.StartsWith("B"))
            return "Basic";

        // Handle Standard tier
        if (sku.StartsWith("S"))
            return "Standard";

        // Handle Premium v2 and v3 tiers
        if (sku.Contains("v2", StringComparison.OrdinalIgnoreCase))
            return "Premiumv2";
        if (sku.Contains("v3", StringComparison.OrdinalIgnoreCase))
            return "Premiumv3";

        return string.Empty;
    }

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
                return new RemediationResult(false,
                    "Cost Calculation",
                    "Current SKU rate not found",
                    OperationId: null,
                    FinishedTime: DateTime.Now);

            if (!HourlyRates.TryGetValue(targetSku, out var targetRate))
                return new RemediationResult(
                    false,
                    "Cost Calculation",
                    "Target SKU rate not found",
                    OperationId: null,
                    FinishedTime: DateTime.Now);

            var hourlyDiff = targetRate - currentRate;
            var dailyDiff = hourlyDiff * 24;
            var monthlyDiff = dailyDiff * 30;

            return new RemediationResult(
                Success: true,
                Action: $"Cost difference for scaling {direction} from {currentSku} to {targetSku}",
                Details: $"Hourly: ${hourlyDiff:F3}\nDaily: ${dailyDiff:F2}\nMonthly: ${monthlyDiff:F2}",
                OperationId: null,
                FinishedTime: DateTime.Now);
        }
        catch (Exception ex)
        {
            return new RemediationResult(
                Success: false,
                Action: "Failed to calculate scaling costs",
                Details: ex.Message,
                OperationId: null,
                FinishedTime: DateTime.Now);
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
    string Details,
    string? OperationId,
    DateTime FinishedTime);
