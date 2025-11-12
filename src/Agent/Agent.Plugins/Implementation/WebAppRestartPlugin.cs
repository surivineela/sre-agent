// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Helpers;
using Agent.Core.Models.Charts;
using Agent.Plugins.Interface;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Newtonsoft.Json.Linq;

namespace Agent.Plugins.Implementation
{
    public class WebAppRestartPlugin(
            IArmPlugin armPlugin,
            ArmHelper armHelper,
            IAppCodeAnalysisPlugin appCodeAnalysisPlugin,
            IAppInsightsPlugin appInsightsPlugin,
            ILogger<WebAppRestartPlugin> logger) : IWebAppRestartPlugin
    {
        private readonly IArmPlugin _armPlugin = armPlugin;
        private readonly ArmHelper _armHelper = armHelper;
        private readonly IAppCodeAnalysisPlugin _appCodeAnalysisPlugin = appCodeAnalysisPlugin;
        private readonly IAppInsightsPlugin _appInsightsPlugin = appInsightsPlugin;
        private readonly ILogger<WebAppRestartPlugin> _logger = logger;

        public async Task<string> GetWebAppRestartExecution(string resourceId)
        {
            _logger.LogInternalInformation("[get_web_app_restart_execution] Invoked with resourceId {resourceId}", resourceId);
            if (string.IsNullOrEmpty(resourceId))
            {
                _logger.LogInternalError("Resource ID is null or empty.");
                return "Invalid resource ID.";
            }

            try
            {
                // Call ArmHelper's GetDetectorResponse to get all restart insights
                var result = await _armHelper.GetDetectorResponseWithTime(resourceId, "webapprestart", DateTime.UtcNow.AddDays(-2));

                // Return ALL insights, not just critical ones
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error getting web app restart execution for {resourceId}", resourceId);
                return $"Failed to retrieve restart execution data: {ex.Message}";
            }
        }

        public async Task<string> GetWebAppCallStacks(string resourceId)
        {
            _logger.LogInternalInformation("[get_web_app_call_stacks] Invoked with resourceId {resourceId}", resourceId);

            try
            {
                // Call AppCodeAnalysisPlugin's GetCallStackForApp
                var callStacks = await _appCodeAnalysisPlugin.GetCallStackForApp(resourceId);

                return callStacks;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error getting web app call stacks for {resourceId}", resourceId);
                return $"Failed to retrieve call stacks: {ex.Message}";
            }
        }

        public async Task<IReadOnlyList<FailedRequestsTimeSeriesData>> GetFailedRequestInvocations(string resourceId, int? minutes = null)
        {
            // Default to 90 minutes if not specified
            int lookbackMinutes = minutes ?? 90;

            // Calculate the start and end times
            DateTime endTime = DateTime.UtcNow;
            DateTime startTime = endTime.AddMinutes(-lookbackMinutes);

            _logger.LogInternalInformation("[GetFailedRequestInvocations] Invoked with resourceId {resourceId}, lookback minutes {lookbackMinutes}", resourceId, lookbackMinutes);

            string resourceName = resourceId;
            if (resourceId.Contains('/'))
            {
                var splitResourceParts = resourceId.Split('/');
                resourceName = splitResourceParts[splitResourceParts.Length - 1];
            }

            string failedRequestsQuery = $@"
                    let start=datetime({startTime:O});
                    let end=datetime({endTime:O});
                    let timeGrain=5m;
                    let dataset=requests
                        | where timestamp > start and timestamp < end
                        | where cloud_RoleName =~ ""{resourceName}""
                        | where client_Type != ""Browser"";
                    dataset
                    | summarize FailedCount=sumif(itemCount, success == false) by name, bin(timestamp, timeGrain)";

            var failedRequestsJson = await _appInsightsPlugin.QueryAppInsightsByWebAppSettings(resourceId, failedRequestsQuery);

            var failedRequestsData = new List<FailedRequestsTimeSeriesData>();

            try
            {
                // Parse the JSON response from App Insights
                var jsonResult = JObject.Parse(failedRequestsJson);

                if (jsonResult["tables"] is JArray tables && tables.Count > 0)
                {
                    var table = tables[0];
                    var columns = table["columns"] as JArray;
                    var rows = table["rows"] as JArray;

                    if (columns != null && rows != null)
                    {
                        // Find the indices of the columns we need
                        int nameIndex = -1;
                        int timestampIndex = -1;
                        int failedCountIndex = -1;

                        for (int i = 0; i < columns.Count; i++)
                        {
                            string columnName = columns[i]["name"]?.ToString().ToLowerInvariant() ?? string.Empty;
                            if (columnName == "name")
                            {
                                nameIndex = i;
                            }
                            else if (columnName == "timestamp")
                            {
                                timestampIndex = i;
                            }
                            else if (columnName == "failedcount")
                            {
                                failedCountIndex = i;
                            }
                        }

                        // Parse each row into a FailedRequestsTimeSeriesData object
                        if (nameIndex >= 0 && timestampIndex >= 0 && failedCountIndex >= 0)
                        {
                            foreach (JArray row in rows)
                            {
                                string requestName = row[nameIndex]?.ToString() ?? "Unknown";
                                DateTime timestamp = row[timestampIndex]?.ToObject<DateTime>() ?? DateTime.MinValue;
                                double failedCount = row[failedCountIndex]?.ToObject<double>() ?? 0;

                                failedRequestsData.Add(new FailedRequestsTimeSeriesData(
                                    TimeStamp: timestamp,
                                    FunctionName: requestName,
                                    FailedCount: failedCount));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error parsing failed requests data for {resourceId}", resourceId);
            }

            var sortedData = failedRequestsData.OrderBy(d => d.TimeStamp).ToList();
            return sortedData;
        }

        public async Task<string> GetTop3Exceptions(string resourceId, DateTime? startTime = null, DateTime? endTime = null)
        {
            // Default to past hour if not specified, with endTime being now minus 15 minutes
            startTime ??= DateTime.UtcNow.AddHours(-1);
            endTime ??= DateTime.UtcNow.AddMinutes(-15);

            string resourceName = resourceId;
            if (resourceId.Contains('/'))
            {
                var splitResourceParts = resourceId.Split('/');
                resourceName = splitResourceParts[splitResourceParts.Length - 1];
            }

            _logger.LogInternalInformation("[GetTop3Exceptions] Invoked with resourceId {resourceId}, startTime {startTime}, endTime {endTime}", resourceId, startTime, endTime);

            string top3ExceptionsQuery = $@"
                    let start=datetime({startTime.Value:O});
                    let end=datetime({endTime.Value:O});
                    let dataset=exceptions
                        | where timestamp > start and timestamp < end
                        | where client_Type != ""Browser""
                        | where cloud_RoleName =~ ""{resourceName}"";
                    dataset
                        | summarize _count=sum(itemCount) by type, outerMessage
                        | sort by _count desc
                        | top 3 by _count";

            var top3Exceptions = await _appInsightsPlugin.QueryAppInsightsByWebAppSettings(resourceId, top3ExceptionsQuery);
            return top3Exceptions;
        }

        [KernelFunction("check_if_resource_is_web_app")]
        [Description("This function checks if a resource is a Web App by verifying its 'kind' property")]
        public async Task<bool> IsWebApp(string resourceId)
        {
            _logger.LogInternalInformation("[is_web_app] Invoked with resourceId {resourceId}", resourceId);
            if (string.IsNullOrEmpty(resourceId))
            {
                _logger.LogInternalError("Resource ID is null or empty.");
                return false;
            }

            try
            {
                // Call ArmPlugin's GetArmResourceAsJson to get the resource details
                var resourceJson = await _armPlugin.GetArmResourceAsJson(resourceId);

                if (string.IsNullOrEmpty(resourceJson))
                {
                    _logger.LogInternalWarning("No resource details found for {resourceId}", resourceId);
                    return false;
                }

                // Parse the JSON and check the 'kind' property
                JObject resourceObj = JObject.Parse(resourceJson);

                if (resourceObj.TryGetValue("kind", out var kindToken))
                {
                    _logger.LogInternalInformation("Found kind token of type {tokenType} with value {tokenValue}",
                        kindToken.GetType().Name, kindToken.ToString());

                    // Handle the case where kindToken is a JArray
                    if (kindToken is JArray kindArray)
                    {
                        // Check if any element in the array contains "app" but not "functionapp"
                        foreach (var item in kindArray)
                        {
                            string kindValue = item.ToString();
                            if (kindValue.Contains("app", StringComparison.OrdinalIgnoreCase) &&
                                !kindValue.Contains("functionapp", StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                        return false;
                    }
                    else
                    {
                        // For non-array tokens (JValue), check if it's a web app but not a function app
                        string kind = kindToken.ToString();
                        _logger.LogInternalInformation("Checking if kind '{kind}' is a web app", kind);
                        return kind.Contains("app", StringComparison.OrdinalIgnoreCase) &&
                               !kind.Contains("functionapp", StringComparison.OrdinalIgnoreCase);
                    }
                }

                _logger.LogInternalWarning("Resource does not have a 'kind' property: {resourceId}", resourceId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error checking if resource is a web app: {resourceId}", resourceId);
                return false;
            }
        }
    }
}
