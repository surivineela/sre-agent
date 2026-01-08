// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Helpers;
using Agent.Core.Models;
using Agent.Plugins.Interface;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Agent.Plugins.Implementation
{
    public class CodeOptimizationsPlugin : ICodeOptimizationsPlugin
    {
        private readonly ArmHelper _armHelper;
        private readonly AppInsightsPlugin _appInsightsPlugin;
        private readonly ILogger<CodeOptimizationsPlugin> _logger;

        public CodeOptimizationsPlugin(
            ArmHelper armHelper,
            AppInsightsPlugin appInsightsPlugin,
            ILogger<CodeOptimizationsPlugin> logger)
        {
            _armHelper = armHelper;
            _appInsightsPlugin = appInsightsPlugin;
            _logger = logger;
        }

        public async Task<Dictionary<string, IEnumerable<InsightsRecommendationContract>>> GetCodeOptimizationInsightsBulkAsync(
            [Description("List of Application Insights resource IDs")] IEnumerable<string> resourceIds
        )
        {
            var apps = new List<AppInfo>();

            // Gather all info for each App Insights resource ID
            foreach (var resourceId in resourceIds)
            {
                try
                {
                    // Parse the resource ID to extract subscription ID, resource group, and resource name
                    var resourceIdentifier = new ResourceIdentifier(resourceId);
                    var subscriptionId = resourceIdentifier.SubscriptionId;
                    var resourceGroupName = resourceIdentifier.ResourceGroupName;
                    var appInsightsName = resourceIdentifier.Name;

                    if (string.IsNullOrEmpty(subscriptionId) || string.IsNullOrEmpty(resourceGroupName) || string.IsNullOrEmpty(appInsightsName))
                    {
                        _logger.LogInternalWarning($"Invalid resource ID format: {resourceId}. Skipping this resource.");
                        continue;
                    }

                    // Get the App Insights AppId using the resource name
                    var appId = await _armHelper.GetAppInsightsAppId(subscriptionId, resourceGroupName, appInsightsName);
                    if (string.IsNullOrEmpty(appId))
                    {
                        _logger.LogInternalWarning($"Could not find AppId for Application Insights resource: {appInsightsName}. Skipping this resource.");
                        continue;
                    }

                    // Construct a resource model with the proper resource ID for portal links
                    var appInsightsResource = new GenericArmResourceModel(
                        id: resourceId,
                        name: appInsightsName,
                        type: "microsoft.insights/components",
                        kind: string.Empty, // App Insights kind is not required for current usage; use empty string
                        location: string.Empty, // Location is not required for current usage; use empty string
                        properties: new object(), // No specific properties are needed; use a non-null default object
                        tags: new Dictionary<string, string>(), //  No tags are required for current usage; use an empty dictionary
                        IdentityModels: new List<GenericArmResourceIdentityModel>() // Identity information is not required; use an empty list
                    );

                    apps.Add(new AppInfo
                    {
                        ResourceId = resourceId, // Use the full resource ID as key
                        SubId = subscriptionId,
                        RoleName = string.Empty, // No role name in this scenario
                        InstrumentationKey = null,
                        AppId = appId,
                        AppInsightsResource = appInsightsResource
                    });
                }
                catch (Exception ex)
                {
                    // Skip this resource and continue with others if fetching app insights fails
                    _logger.LogInternalWarning(ex, $"Failed to get Application Insights info for resource {resourceId}. Skipping this resource.");
                    continue;
                }
            }

            var appIds = apps
                .Where(x => !string.IsNullOrEmpty(x.AppId))
                .Select(x => x.AppId!)
                .Distinct()
                .ToList();

            if (!appIds.Any())
                return new Dictionary<string, IEnumerable<InsightsRecommendationContract>>();

            var bulkRequest = new BulkInsightsPostBodyContract { Apps = appIds };

            // Set time range (last 24 hours)
            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddDays(-1);

            // Call the bulk API
            var bulkInsights = await _armHelper.GetCodeOptimizationsInsightsBulkAsync(
                bulkRequest,
                startTime,
                endTime
            );

            var result = new Dictionary<string, IEnumerable<InsightsRecommendationContract>>();

            if (bulkInsights != null)
            {
                // Build lookup dictionary by AppId only for matching
                var insightsByAppId = bulkInsights
                    .GroupBy(b => b.AppId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var app in apps)
                {
                    if (!Guid.TryParse(app.AppId, out var appGuid))
                    {
                        result[app.ResourceId] = Enumerable.Empty<InsightsRecommendationContract>();
                        continue;
                    }

                    List<AggregatedInsightsContract> insights;
                    // Match by AppId only
                    if (insightsByAppId.TryGetValue(appGuid, out var appInsights))
                    {
                        insights = appInsights;
                    }
                    else
                    {
                        insights = new List<AggregatedInsightsContract>();
                    }

                    var processed = ProcessInsights(insights, app.SubId, app.AppInsightsResource);
                    result[app.ResourceId] = processed;
                }
            }

            return result;
        }

        private IEnumerable<InsightsRecommendationContract> ProcessInsights(IEnumerable<AggregatedInsightsContract> insights, string subId, GenericArmResourceModel? appInsightsResource)
        {
            // Filter the insights to grab the top 3.
            IEnumerable<AggregatedInsightsContract> filteredInsights = insights
                .Where(i => i.Value > 1)
                .OrderByDescending(i => i.Criteria)
                .Take(3);

            var recommendations = new List<InsightsRecommendationContract>();

            foreach (var insight in filteredInsights)
            {
                var portalLink = GetPortalLink(insight, subId, insight.AppId.ToString(), appInsightsResource);
                var recommendation = new InsightsRecommendationContract
                {
                    PerformanceIssue = GetPerformanceIssue(insight),
                    CurrentCondition = GetCurrentCondition(insight),
                    Type = insight.IssueCategory,
                    PortalLink = portalLink,
                    ImpactPercent = GetImpactValue(insight),
                    RoleName = insight.RoleName ?? string.Empty,
                    AppId = insight.AppId.ToString()
                };
                recommendations.Add(recommendation);
            }

            return recommendations;
        }

        private string GetRoleName(string resourceId, JObject appSettings)
        {
            string roleName;
            // if the setting WEBSITE_CLOUD_ROLENAME is present use that as role name, if not use the site name from resourceId
            if (appSettings["properties"]?["WEBSITE_CLOUD_ROLENAME"] != null)
            {
                roleName = appSettings["properties"]?["WEBSITE_CLOUD_ROLENAME"]?.ToString() ?? string.Empty;
            }
            else
            {
                // Get the site name from the resourceId string
                roleName = resourceId.Split('/')[8];
            }
            return roleName;
        }

        private string GetPortalLink(AggregatedInsightsContract insight, string subscriptionId, string? appInsightsAppId, GenericArmResourceModel? appInsightsResource)
        {
            if (appInsightsResource == null || string.IsNullOrEmpty(appInsightsAppId))
            {
                return string.Empty;
            }

            string resourceId = appInsightsResource.id;
            var resourceIdentifier = new ResourceIdentifier(resourceId);
            string resourceGroup = resourceIdentifier.ResourceGroupName ?? string.Empty;

            var componentObj = new
            {
                SubscriptionId = subscriptionId,
                ResourceGroup = resourceGroup,
                Name = appInsightsResource.name,
                LinkedApplicationType = 0,
                ResourceId = resourceId,
                ResourceType = appInsightsResource.type?.ToLowerInvariant() ?? "microsoft.insights/components",
                IsAzureFirst = false
            };

            string componentJson = System.Text.Json.JsonSerializer.Serialize(componentObj);
            string encodedComponent = Uri.EscapeDataString(componentJson);
            string openedFrom = "azure-sre-agent";
            return $"https://ms.portal.azure.com/#view/Microsoft_Azure_CodeOptimizations/CodeOptimizationsBlade/ComponentId~/{encodedComponent}/AppId/{appInsightsAppId}/OpenedFrom/{openedFrom}/key/{insight.Key}";
        }

        private string GetPerformanceIssue(AggregatedInsightsContract insight)
        {
            if (insight.IssueCategory == "CPU")
            {
                return $"High CPU usage detected in function {insight.Function}";
            }
            else if (insight.IssueCategory == "Memory")
            {
                return $"Excessive allocations due to {insight.Function}";
            }
            else if (insight.IssueCategory == "Blocking")
            {
                return $"Excessive thread blocking by {insight.Function}";
            }
            return "";
        }

        private string GetCurrentCondition(AggregatedInsightsContract insight)
        {
            if (insight.IssueCategory == "CPU")
            {
                return $"{insight.Value:F2}% of your CPU was spent in {insight.Function} called from {insight.ParentFunction}. The expected value is less than {Math.Round(insight.Criteria, 2)}%.";
            }
            else if (insight.IssueCategory == "Memory")
            {
                return $"{insight.Value:F2}% of your memory was spent in {insight.Function} called from {insight.ParentFunction}. The expected value is less than {Math.Round(insight.Criteria, 2)}%.";
            }
            else if (insight.IssueCategory == "Blocking")
            {
                return $"{insight.Value / 1000:F2} seconds of your thread time was spent in {insight.Function} called from {insight.ParentFunction}. The expected value is less than {Math.Round(insight.Criteria / 1000, 2)} seconds.";
            }
            return "";
        }

        private string GetImpactValue(AggregatedInsightsContract insight)
        {
            if (insight.IssueCategory == "CPU" || insight.IssueCategory == "Memory")
            {
                return $"{Math.Round(insight.Value, 2)}%";
            }
            else if (insight.IssueCategory == "Blocking")
            {
                return $"{Math.Round(insight.Value / 1000, 2)} s";
            }
            return "";
        }
    }
}
