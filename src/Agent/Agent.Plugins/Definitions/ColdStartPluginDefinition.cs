// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Framework;
using Agent.Plugins.Interface;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(IsFirstPartyOnly = true)]
    public class ColdStartPluginDefinition
    {
        private readonly IKustoPluginChat _kustoPlugin;

        public ColdStartPluginDefinition(IKustoPluginChat kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }

        [Description(@"""
Finds general information about the HTTP request for cold start analysis.
Use this tool to get basic request information including site name, URL, activity ID, and timestamps.
Automatically selects the appropriate data source based on the request age (Analytics for older requests, WAWS for recent requests).
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- KustoCluster: The Kusto cluster where the data was found.
- ConsumptionType: Type of consumption (Windows Consumption, Flex Consumption, Linux Consumption).
- TIMESTAMP: Timestamp of the request.
- S_sitename: Site name.
- ActivityId: Activity ID of the request.
- Time_taken: Time taken for the request.
- UrlRewriteTime: URL rewrite time.
- ArrTime: ARR time.
- DSCallTime: DS call time.
- Sc_status: Status code.
- Cs_method: HTTP method.
- Cs_uri_stem: URI stem.
- EventPrimaryStampName: Primary stamp name.
"""
)]
        [AgentTool(ToolMode.Auto)]
        public Task<string> FindRequestGeneralInfo(
            [Description("Site name.")] string siteName,
            [Description("Request URL.")] string url,
            [Description("Activity ID of the request.")] string activityId,
            [Description("UTC date time of the request.")] string utcDateTime)
        {
            // Validate the UTC date time
            if (!DateTime.TryParse(utcDateTime, out var utcDateTimeParsed))
            {
                throw new ArgumentException($"Invalid DateTime: {utcDateTime}");
            }

            // If the UTC date time is older than 30 hours, use the analytics query for better performance
            var functionName = utcDateTimeParsed <= DateTime.Now.AddHours(-30)
                ? "ColdStart.FindRequestGeneralInfoFromAnalytics"
                : "ColdStart.FindRequestGeneralInfoFromWaws";

            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync(functionName, "wawscus", "wawsprod",
            new Dictionary<string, string> {
                { "siteName", siteName },
                { "url", url },
                { "activityId", activityId },
                { "utcDateTime", utcDateTime }
            });
        }

        [Description(@"""
Finds breakdown of the HTTP cold start request for detailed analysis.
Use this tool to get detailed cold start request breakdown based on consumption type.
Automatically selects the appropriate query based on the consumption type (Windows, Flex, or Linux).
Output: Returns tab-separated table data in CSV format. The first line contains column headers specific to the consumption type.
"""
)]
        [AgentTool(ToolMode.Auto)]
        public Task<string> GetColdStartRequestDetails(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Consumption type (Windows Consumption, Flex Consumption, Linux Consumption).")] string consumptionType,
            [Description("Activity ID of the request.")] string activityId,
            [Description("UTC date time of the request.")] string utcDateTime)
        {
            // Determine the appropriate function based on consumption type
            string functionName;
            if (consumptionType.Contains("Windows Consumption", StringComparison.OrdinalIgnoreCase))
            {
                functionName = "ColdStart.GetColdStartRequestDetailsForWindowsConsumption";
            }
            else if (consumptionType.Contains("Flex Consumption", StringComparison.OrdinalIgnoreCase))
            {
                functionName = "ColdStart.GetColdStartRequestDetailsForFlexConsumption";
            }
            else if (consumptionType.Contains("Linux Consumption", StringComparison.OrdinalIgnoreCase))
            {
                functionName = "ColdStart.GetColdStartRequestDetailsForLinuxConsumption";
            }
            else
            {
                throw new ArgumentException($"Unsupported consumption type: {consumptionType}");
            }

            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync(functionName, clusterName, "wawsprod",
            new Dictionary<string, string> {
                { "clusterName", clusterName },
                { "consumptionType", consumptionType },
                { "activityId", activityId },
                { "utcDateTime", utcDateTime }
            });
        }

        [Description(@"""
Finds breakdown of the HTTP cold start request from Legion cluster.
Use this tool to get detailed cold start request breakdown from Legion for Flex Consumption.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- SpecializationTime: Time when specialization started.
- PodName: Name of the pod.
- Tenant: Tenant information.
- json: JSON payload with detailed metrics.
- LegionStampName: Legion stamp name.
- CenturionRoleId: Centurion role ID.
- env_dt_traceId: Environment trace ID.
- PADownloadAndUnzip: Pod agent download and unzip duration.
- PADownloadContentBody: Pod agent download content body duration.
- PAUnzip: Pod agent unzip duration.
"""
)]
        [AgentTool(ToolMode.Auto)]
        public Task<string> GetColdStartRequestDetailsFromLegion(
            [Description("Legion cluster name.")] string legionClusterName,
            [Description("Pod name.")] string podName,
            [Description("UTC date time of the request.")] string utcDateTime)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("ColdStart.GetColdStartRequestDetailsFromLegion", legionClusterName, "legion",
            new Dictionary<string, string> {
                { "legionClusterName", legionClusterName },
                { "podName", podName },
                { "utcDateTime", utcDateTime }
            });
        }

        [Description(@"""
Shows cold start trends for SLA sites over a specified time period.
Use this tool to analyze cold start performance trends for SLA sites.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- pdate: Date of the measurement.
- P50: 50th percentile cold start time.
- P99: 99th percentile cold start time.
"""
)]
        [AgentTool(ToolMode.Auto)]
        public Task<string> GetColdStartDetailsForSlaSites(
            [Description("Number of days to look back.")] int days = 120,
            [Description("Platform to filter by (Legion, Windows, etc.).")] string platform = "Legion",
            [Description("Stack to filter by (dotnet, etc.).")] string stack = "")
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("ColdStart.GetColdStartDetailsForSlaSites", "wawsaneus.eastus", "wawsanprod",
            new Dictionary<string, string> {
                { "days", days.ToString() },
                { "platform", platform },
                { "stack", stack }
            });
        }

        [Description(@"""
Shows profile data for production cold start SLA sites.
Use this tool to get aggregated profile data for cold start performance analysis.
Output: Returns tab-separated table data in CSV format. The first line contains column headers for various performance metrics.
"""
)]
        [AgentTool(ToolMode.Auto)]
        public Task<string> GetColdStartProfileData()
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("ColdStart.GetColdStartProfileData", "wawseus", "wawsprod",
            new Dictionary<string, string>());
        }

        [Description(@"""
Shows detailed profile data for production cold start SLA sites.
Use this tool to get detailed profile data including JIT, memory, and other performance metrics.
Output: Returns tab-separated table data in CSV format. The first line contains column headers for detailed performance metrics.
"""
)]
        [AgentTool(ToolMode.Auto)]
        public Task<string> GetColdStartProfileDataDetails()
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("ColdStart.GetColdStartProfileDataDetails", "wawseus", "wawsprod",
            new Dictionary<string, string>());
        }

        [Description(@"""
Runs the cold start regression analysis to detect performance regressions.
Use this tool to analyze cold start performance regressions by stage.
Output: Returns tab-separated table data in CSV format. The first line contains column headers for regression analysis.
"""
)]
        [AgentTool(ToolMode.Auto)]
        public Task<string> RunColdStartRegressionAnalysis()
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("ColdStart.RunColdStartRegressionAnalysis", "wawscus", "wawsprod",
            new Dictionary<string, string>());
        }

        [Description(@"""
Runs the cold start regression analysis per region to detect regional performance differences.
Use this tool to analyze cold start performance regressions broken down by Azure region.
Output: Returns tab-separated table data in CSV format. The first line contains column headers for regional regression analysis.
"""
)]
        [AgentTool(ToolMode.Auto)]
        public Task<string> RunColdStartRegressionAnalysisPerRegion()
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("ColdStart.RunColdStartRegressionAnalysisPerRegion", "wawscus", "wawsprod",
            new Dictionary<string, string>());
        }
    }
}
