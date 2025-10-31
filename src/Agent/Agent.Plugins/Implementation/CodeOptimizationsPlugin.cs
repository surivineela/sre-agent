// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Helpers;
using Agent.Core.Models;
using Agent.Plugins.Interface;
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

        [Description("Get code optimization insights")]
        public async Task<IEnumerable<InsightsRecommendationContract>> GetCodeOptimizationInsightsAsync(
            [Description("resourceId of app service")] string resourceId
        )
        {
            // get instrumentation Key from web app settings
            var appSettings = await _armHelper.GetAppSettings(resourceId);
            var jsonObject = JObject.Parse(appSettings);
            var instrumentationKey = _appInsightsPlugin.GetInstrumentationKey(jsonObject["properties"]?["APPINSIGHTS_INSTRUMENTATIONKEY"]?.ToString()) ?? _appInsightsPlugin.GetInstrumentationKey(jsonObject["properties"]?["APPLICATIONINSIGHTS_CONNECTION_STRING"]?.ToString());

            var subId = resourceId.Split('/')[2];
            var roleName = GetRoleName(resourceId, jsonObject);

            // use instrumentation key to single in on the correct app insights resource
            var appInsightsAppId = await _armHelper.GetAppInsightsAppIdBySubscription(subId, instrumentationKey ?? string.Empty);

            // Retrieve full App Insights resource metadata for constructing deep links
            var appInsightsResource = await _armHelper.GetAppInsightsResourceByInstrumentationKeyAsync(subId, instrumentationKey ?? string.Empty);

            // Retrieve insights for the app insights resource.
            var insights = await _armHelper.GetCodeOptimizationsInsightsAsync(appInsightsAppId, roleName);
            var processedInsights = ProcessInsights(insights, subId, appInsightsResource);
            return processedInsights;
        }

        public async Task<Dictionary<string, IEnumerable<InsightsRecommendationContract>>> GetCodeOptimizationInsightsBulkAsync(
            [Description("List of resourceIds of app services")] IEnumerable<string> resourceIds
        )
        {
            var apps = new List<AppInfo>();

            // Gather all info for each resource
            foreach (var resourceId in resourceIds)
            {
                try
                {
                    var appSettings = await _armHelper.GetAppSettings(resourceId);
                    var jsonObject = JObject.Parse(appSettings);
                    var instrumentationKey = _appInsightsPlugin.GetInstrumentationKey(jsonObject["properties"]?["APPINSIGHTS_INSTRUMENTATIONKEY"]?.ToString())
                        ?? _appInsightsPlugin.GetInstrumentationKey(jsonObject["properties"]?["APPLICATIONINSIGHTS_CONNECTION_STRING"]?.ToString());
                    var subId = resourceId.Split('/')[2];
                    var roleName = GetRoleName(resourceId, jsonObject);
                    var appId = await _armHelper.GetAppInsightsAppIdBySubscription(subId, instrumentationKey ?? string.Empty);
                    var appInsightsResource = await _armHelper.GetAppInsightsResourceByInstrumentationKeyAsync(subId, instrumentationKey ?? string.Empty);
                    apps.Add(new AppInfo
                    {
                        ResourceId = resourceId,
                        SubId = subId,
                        RoleName = roleName,
                        InstrumentationKey = instrumentationKey,
                        AppId = appId,
                        AppInsightsResource = appInsightsResource
                    });
                }
                catch (Exception ex)
                {
                    // Skip this resource and continue with others if fetching app settings fails
                    _logger.LogInternalWarning(ex, $"Failed to get app settings for resource {resourceId}. Skipping this resource.");
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
                // Build lookup dictionaries for faster matching
                var insightsByRoleNameAndAppId = bulkInsights
                    .GroupBy(b => (b.AppId, b.RoleName))
                    .ToDictionary(g => g.Key, g => g.ToList());
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
                    // Try to match by (AppId, RoleName) first
                    if (insightsByRoleNameAndAppId.TryGetValue((appGuid, app.RoleName), out var byRoleName))
                    {
                        insights = byRoleName;
                    }
                    // If not found, fall back to matching by AppId only
                    else if (insightsByAppId.TryGetValue(appGuid, out var byAppId))
                    {
                        insights = byAppId;
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
            string resourceGroup = string.Empty;
            var segments = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (int i = 0; i < segments.Length - 1; i++)
            {
                if (segments[i].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase) && i + 1 < segments.Length)
                {
                    resourceGroup = segments[i + 1];
                    break;
                }
            }

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
            return $"https://ms.portal.azure.com/#view/Microsoft_Azure_CodeOptimizations/CodeOptimizationsBlade/ComponentId~/{encodedComponent}/AppId/{appInsightsAppId}/OpenedFrom/{openedFrom}";
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
