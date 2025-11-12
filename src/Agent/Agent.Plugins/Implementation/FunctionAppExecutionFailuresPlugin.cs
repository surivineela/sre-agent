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
    public class FunctionAppExecutionFailuresPlugin(
            IArmPlugin armPlugin,
            ArmHelper armHelper,
            IAppCodeAnalysisPlugin appCodeAnalysisPlugin,
            IAppInsightsPlugin appInsightsPlugin,
            ILogger<FunctionAppExecutionFailuresPlugin> logger) : IFunctionAppExecutionFailuresPlugin
    {
        private readonly IArmPlugin _armPlugin = armPlugin;
        private readonly ArmHelper _armHelper = armHelper;
        private readonly IAppCodeAnalysisPlugin _appCodeAnalysisPlugin = appCodeAnalysisPlugin;
        private readonly IAppInsightsPlugin _appInsightsPlugin = appInsightsPlugin;
        private readonly ILogger<FunctionAppExecutionFailuresPlugin> _logger = logger;

        /// <summary>
        /// Determines the appropriate time grain based on the time range to avoid returning too many results
        /// </summary>
        /// <param name="startTime">Start time of the query</param>
        /// <param name="endTime">End time of the query</param>
        /// <returns>Time grain string (5m, 10m, or 1d)</returns>
        private static string GetTimeGrain(DateTime startTime, DateTime endTime)
        {
            var timeSpan = endTime - startTime;

            // For time ranges less than or equal to 6 hours, use 5 minute grain
            if (timeSpan.TotalHours <= 6)
            {
                return "5m";
            }
            // For time ranges less than or equal to 24 hours, use 10 minute grain
            else if (timeSpan.TotalHours <= 24)
            {
                return "10m";
            }
            // For longer time ranges, use 1 day grain
            else
            {
                return "1d";
            }
        }

        public async Task<string> GetFunctionAppExecutionFailures(string resourceId)
        {
            _logger.LogInternalInformation("[get_function_app_execution_failures] Invoked with resourceId {resourceId}", resourceId);
            if (string.IsNullOrEmpty(resourceId))
            {
                _logger.LogInternalError("Resource ID is null or empty.");
                return "Invalid resource ID.";
            }

            try
            {
                // Call ArmHelper's GetDetectorResponse instead of _armPlugin.GetArmResourceLogs
                var result = await _armHelper.GetDetectorResponseWithTime(resourceId, "functionExecutionErrors");

                // Check if the response is too large (roughly over 50KB)
                const int maxResponseSize = 50 * 1024; // 50KB threshold
                if (result.Length > maxResponseSize)
                {
                    _logger.LogInternalInformation("Response size {size} bytes exceeds threshold, extracting only critical function failures", result.Length);

                    try
                    {
                        // Parse the JSON response
                        JObject resultObj = JObject.Parse(result);

                        // Look for the critical failures table - find the dataset array
                        if (resultObj["properties"] is JObject properties &&
                            properties["dataset"] is JArray dataset)
                        {
                            // Look for the table with critical failures
                            foreach (var item in dataset)
                            {
                                if (item["table"] is JObject table)
                                {
                                    var rows = table["rows"] as JArray;
                                    if (rows != null && rows.Count > 0)
                                    {
                                        // Check if this is the table with critical failures
                                        // The first row usually contains the status, message, etc.
                                        var firstRow = rows[0] as JArray;
                                        if (firstRow != null && firstRow.Count > 1)
                                        {
                                            string status = firstRow[0]?.ToString() ?? string.Empty;
                                            string message = firstRow[1]?.ToString() ?? string.Empty;

                                            if (status == "Critical" &&
                                                message == "Detected function(s) having execution failure rate more than 1%.")
                                            {
                                                // This is the table we want to keep - extract just this part
                                                var reducedResponse = new JObject
                                                {
                                                    ["id"] = resultObj["id"],
                                                    ["name"] = resultObj["name"],
                                                    ["type"] = resultObj["type"],
                                                    ["location"] = resultObj["location"],
                                                    ["properties"] = new JObject
                                                    {
                                                        ["metadata"] = properties["metadata"],
                                                        ["dataset"] = new JArray { item }
                                                    }
                                                };

                                                _logger.LogInternalInformation("Successfully extracted critical failure data from large response");
                                                return reducedResponse.ToString();
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        _logger.LogInternalWarning("Failed to extract critical failures table from large response, returning full response");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalWarning(ex, "Error while attempting to extract critical failures from large response, returning full response");
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error getting function app execution failures for {resourceId}", resourceId);
                return $"Failed to retrieve execution failures: {ex.Message}";
            }
        }

        public async Task<string> GetFunctionAppCallStacks(string resourceId)
        {
            _logger.LogInternalInformation("[get_function_app_call_stacks] Invoked with resourceId {resourceId}", resourceId);

            try
            {
                // Call AppCodeAnalysisPlugin's GetCallStackForApp
                var callStacks = await _appCodeAnalysisPlugin.GetCallStackForApp(resourceId);

                return callStacks;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error getting function app call stacks for {resourceId}", resourceId);
                return $"Failed to retrieve call stacks: {ex.Message}";
            }
        }

        public async Task<IReadOnlyList<FailedRequestsTimeSeriesData>> GetFailedFunctionInvocations(string resourceId, int? minutes = null)
        {
            // Default to 60 minutes if not specified
            int lookbackMinutes = minutes ?? 60;

            // Calculate the start and end times
            DateTime endTime = DateTime.UtcNow;
            DateTime startTime = endTime.AddMinutes(-lookbackMinutes);

            _logger.LogInternalInformation("[GetFailedFunctionInvocations] Invoked with resourceId {resourceId}, lookback minutes {lookbackMinutes}", resourceId, lookbackMinutes);

            string resourceName = resourceId;
            if (resourceId.Contains('/'))
            {
                var splitResourceParts = resourceId.Split('/');
                resourceName = splitResourceParts[splitResourceParts.Length - 1];
            }

            // Determine timeGrain based on the time range
            string timeGrain = GetTimeGrain(startTime, endTime);

            string failedRequestsPerFunctionQuery = $@"
                    let start=datetime({startTime:O});
                    let end=datetime({endTime:O});
                    let timeGrain={timeGrain};
                    let dataset=requests
                        | where timestamp > start and timestamp < end
                        | where cloud_RoleName =~ ""{resourceName}""
                        | where client_Type != ""Browser"";
                    dataset
                    | summarize FailedCount=sumif(itemCount, success == false) by name, bin(timestamp, timeGrain)";

            var failedRequestsPerFunctionJson = await _appInsightsPlugin.QueryAppInsightsByWebAppSettings(resourceId, failedRequestsPerFunctionQuery);

            var failedRequestsData = new List<FailedRequestsTimeSeriesData>();

            try
            {
                // Parse the JSON response from App Insights
                var jsonResult = JObject.Parse(failedRequestsPerFunctionJson);

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
                            string columnName = columns[i]["name"]?.ToString()?.ToLowerInvariant() ?? string.Empty;

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
                                string functionName = row[nameIndex]?.ToString() ?? "Unknown";
                                DateTime timestamp = row[timestampIndex]?.ToObject<DateTime>() ?? DateTime.MinValue;
                                double failedCount = row[failedCountIndex]?.ToObject<double>() ?? 0;

                                failedRequestsData.Add(new FailedRequestsTimeSeriesData(
                                    TimeStamp: timestamp,
                                    FunctionName: functionName,
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

        public async Task<string> GetTop3ExceptionsPerFunction(string resourceId, DateTime? startTime = null, DateTime? endTime = null)
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
            _logger.LogInternalInformation("[GetTop3ExceptionsPerFunction] Invoked with resourceId {resourceId}, startTime {startTime}, endTime {endTime}", resourceId, startTime, endTime);

            // Determine timeGrain based on the time range
            string timeGrain = GetTimeGrain(startTime.Value, endTime.Value);

            string top3ExceptionsPerFunctionQuery = $@"
                    let start=datetime({startTime.Value:O});
                    let end=datetime({endTime.Value:O});
                    let timeGrain={timeGrain};
                    let dataset=exceptions
                        | where timestamp > start and timestamp < end
                        | where client_Type != ""Browser""
                        | where cloud_RoleName =~ ""{resourceName}""
                        | extend FunctionName = iif(outerMessage has ""Result: Function"", extract(@""Result: Function '([^']+)'"", 1, outerMessage), """")
                        | extend FunctionName = iif(isempty(FunctionName), iif(method has "".Run"", extract(@""([^.]+).([^.]+).Run"", 2, method), method), FunctionName)
                        | extend FunctionName = iif(isempty(FunctionName),method,FunctionName)
                        | extend FunctionName = iif(isempty(FunctionName),extract(@""Function '([^']+)'"", 1, outerMessage),FunctionName)
                        | parse outerMessage with * ""Exception: "" ExceptionType "":"" ExceptionMessage ""\n"" StackTrace;
                    dataset
                        | extend ExceptionOrType = iif(isempty(ExceptionType), type, ExceptionType)
                        | summarize _count = sum(itemCount), 
                                   ExceptionMessage = any(iif(isempty(ExceptionMessage), message, ExceptionMessage)),
                                   StackTrace = any(iif(isempty(StackTrace), details, StackTrace))
                          by ExceptionOrType
                        | sort by _count desc
                        | top 3 by _count
                        | project ExceptionType = ExceptionOrType, ExceptionMessage, StackTrace, Count = _count";

            var top3ExceptionsPerFunction = await _appInsightsPlugin.QueryAppInsightsByWebAppSettings(resourceId, top3ExceptionsPerFunctionQuery);
            return top3ExceptionsPerFunction;
        }

        public async Task<string> GetTop3ExceptionsWithStackTraces(string resourceId, DateTime? startTime = null, DateTime? endTime = null)
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
            _logger.LogInternalInformation("[GetTop3ExceptionsWithStackTraces] Invoked with resourceId {resourceId}, startTime {startTime}, endTime {endTime}", resourceId, startTime, endTime);

            // Determine timeGrain based on the time range
            string timeGrain = GetTimeGrain(startTime.Value, endTime.Value);

            string top3ExceptionsWithStackTracesQuery = $@"
                    let start=datetime({startTime.Value:O});
                    let end=datetime({endTime.Value:O});
                    let timeGrain={timeGrain};
                    let dataset=exceptions
                        | where timestamp > start and timestamp < end
                        | where client_Type != ""Browser""
                        | where cloud_RoleName =~ ""{resourceName}""
                        | extend FunctionName = iif(outerMessage has ""Result: Function"", extract(@""Result: Function '([^']+)'"", 1, outerMessage), """")
                        | extend FunctionName = iif(isempty(FunctionName), iif(method has "".Run"", extract(@""([^.]+).([^.]+).Run"", 2, method), method), FunctionName)
                        | extend FunctionName = iif(isempty(FunctionName),method,FunctionName)
                        | extend FunctionName = iif(isempty(FunctionName),extract(@""Function '([^']+)'"", 1, outerMessage),FunctionName)
                        | parse outerMessage with * ""Exception: "" ExceptionType "":"" ExceptionMessage ""\n"" StackTrace;
                    dataset
                        | extend ExceptionOrType = iif(isempty(ExceptionType), type, ExceptionType)
                        | extend FullExceptionMessage = iif(isempty(ExceptionMessage), message, ExceptionMessage)
                        | extend FullStackTrace = iif(isempty(StackTrace), details, StackTrace)
                        | summarize _count = sum(itemCount), 
                                   ExceptionMessages = make_list(FullExceptionMessage, 3),
                                   StackTraces = make_list(FullStackTrace, 3),
                                   FunctionNames = make_list(FunctionName, 3)
                          by ExceptionOrType
                        | sort by _count desc
                        | top 3 by _count
                        | project ExceptionType = ExceptionOrType, ExceptionMessages, StackTraces, FunctionNames, Count = _count";

            var top3ExceptionsWithStackTraces = await _appInsightsPlugin.QueryAppInsightsByWebAppSettings(resourceId, top3ExceptionsWithStackTracesQuery);
            return top3ExceptionsWithStackTraces;
        }

        public async Task<string> GetHostRuntimeErrorEvents(string resourceId, DateTime? startTime = null, DateTime? endTime = null)
        {
            _logger.LogInternalInformation("[get_host_runtime_error_events] Invoked with resourceId {resourceId}", resourceId);
            if (string.IsNullOrEmpty(resourceId))
            {
                _logger.LogInternalError("Resource ID is null or empty.");
                return "Invalid resource ID.";
            }

            try
            {
                // Parse the resourceId to get subscription ID and resource group
                var parts = resourceId.Split('/');

                if (parts.Length < 9)
                {
                    return "Invalid resource ID format.";
                }

                string subscriptionId = parts[2];
                string resourceGroup = parts[4];

                // Format timestamps if provided
                string formattedStartTime = startTime.HasValue ? startTime.Value.ToString("yyyy-MM-ddTHH:mm:ssZ") : string.Empty;
                string formattedEndTime = endTime.HasValue ? endTime.Value.ToString("yyyy-MM-ddTHH:mm:ssZ") : string.Empty;

                // Call ArmHelper to get critical/error/warning activity logs
                var activityLogs = await _armHelper.GetCriticalErrorActivityLogs(
                    subscriptionId,
                    resourceGroup,
                    resourceId,
                    formattedStartTime,
                    formattedEndTime);

                if (activityLogs == null || activityLogs.Count == 0)
                {
                    return "No error events found in the specified time range.";
                }

                // Return all activity logs without filtering for host runtime errors
                var response = new
                {
                    TotalErrorCount = activityLogs.Count,
                    ErrorEvents = activityLogs.Select(e => new
                    {
                        e.Timestamp,
                        Operation = e.OperationName,
                        e.ErrorMessage,
                        e.Status,
                        e.StatusMessage,
                        e.Caller,
                        e.ResourceId,
                        e.IsSuccessful
                    }).ToList()
                };

                return Newtonsoft.Json.JsonConvert.SerializeObject(response, Newtonsoft.Json.Formatting.Indented);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error getting error events for {resourceId}", resourceId);
                return $"Failed to retrieve error events: {ex.Message}";
            }
        }

        [KernelFunction("check_if_resource_is_function_app")]
        [Description("This function checks if a resource is a Function App by verifying its 'kind' property contains 'functionapp'")]
        public async Task<bool> IsFunctionApp(string resourceId)
        {
            _logger.LogInternalInformation("[is_function_app] Invoked with resourceId {resourceId}", resourceId);
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
                        // Check if any element in the array contains "functionapp"
                        foreach (var item in kindArray)
                        {
                            if (item.ToString().Contains("functionapp", StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                        return false;
                    }
                    else
                    {
                        // For non-array tokens (JValue), just check if the string contains "functionapp"
                        // This handles comma-separated values like "{functionapp,linux}"
                        string kind = kindToken.ToString();
                        _logger.LogInternalInformation("Checking if kind '{kind}' contains 'functionapp'", kind);
                        return kind.Contains("functionapp", StringComparison.OrdinalIgnoreCase);
                    }
                }

                _logger.LogInternalWarning("Resource does not have a 'kind' property: {resourceId}", resourceId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error checking if resource is a function app: {resourceId}", resourceId);
                return false;
            }
        }

        [KernelFunction("check_if_resource_has_host_runtime_errors")]
        [Description("This function checks if a Function App has host runtime related errors in its activity logs")]
        public async Task<bool> HasHostRuntimeErrors(string resourceId, DateTime? startTime = null, DateTime? endTime = null)
        {
            _logger.LogInternalInformation("[has_host_runtime_errors] Invoked with resourceId {resourceId}", resourceId);
            if (string.IsNullOrEmpty(resourceId))
            {
                _logger.LogInternalError("Resource ID is null or empty.");
                return false;
            }

            try
            {
                const string hostRuntimeErrorMessage = "Encountered an error (InternalServerError) from host runtime";

                // First, try synchronizing the function app host to check for runtime errors
                _logger.LogInternalInformation("Attempting to sync Function App host to check for runtime errors");
                string syncResponse = await _armHelper.SyncFunctionAppHost(resourceId);

                // Check if the sync response contains the specific host runtime error message
                if (!string.IsNullOrEmpty(syncResponse))
                {
                    try
                    {
                        var syncResponseObj = JObject.Parse(syncResponse);

                        // Check if the response contains the error message we're looking for
                        if (syncResponseObj["responses"] is JArray responses && responses.Count > 0)
                        {
                            foreach (var response in responses)
                            {
                                if (response["content"] is JObject content)
                                {
                                    string message = content["Message"]?.ToString() ?? string.Empty;

                                    if (message.Contains(hostRuntimeErrorMessage, StringComparison.OrdinalIgnoreCase))
                                    {
                                        _logger.LogInternalInformation("Found host runtime error from sync response: {message}", message);
                                        return true;
                                    }

                                    // Also check for the error in Details array
                                    if (content["Details"] is JArray details)
                                    {
                                        foreach (var detail in details)
                                        {
                                            string detailMessage = detail["Message"]?.ToString() ?? string.Empty;
                                            if (detailMessage.Contains(hostRuntimeErrorMessage, StringComparison.OrdinalIgnoreCase))
                                            {
                                                _logger.LogInternalInformation("Found host runtime error in details: {message}", detailMessage);
                                                return true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalWarning(ex, "Error parsing sync response JSON, falling back to checking activity logs");
                    }
                }

                // If no host runtime errors found in sync response, proceed with checking activity logs

                // Get the host runtime errors
                var hostRuntimeErrorsJson = await GetHostRuntimeErrorEvents(resourceId, startTime, endTime);

                if (string.IsNullOrEmpty(hostRuntimeErrorsJson) || hostRuntimeErrorsJson.Contains("No error events found"))
                {
                    return false;
                }

                // Parse the JSON and check for host runtime errors
                JObject errorsObj = JObject.Parse(hostRuntimeErrorsJson);

                if (errorsObj.TryGetValue("ErrorEvents", out var eventsToken) && eventsToken is JArray eventsArray)
                {
                    // Check each event for host runtime error indicators
                    foreach (var eventItem in eventsArray)
                    {
                        string errorMessage = eventItem["ErrorMessage"]?.ToString() ?? "";
                        string operation = eventItem["Operation"]?.ToString() ?? "";

                        // Check the status message which might contain detailed error info
                        if (eventItem != null && eventItem["StatusMessage"] != null &&
                            !string.IsNullOrEmpty(eventItem["StatusMessage"]!.ToString()))
                        {
                            try
                            {
                                var statusMessageString = eventItem["StatusMessage"]!.ToString();
                                // Try to parse the StatusMessage as JSON which contains more detailed error info
                                var statusMessageObj = JObject.Parse(statusMessageString);

                                // Check Message property in the StatusMessage JSON
                                string statusMessageContent = statusMessageObj["Message"]?.ToString() ?? "";

                                // Check if Details array exists and contains error messages
                                if (statusMessageObj["Details"] is JArray detailsArray && detailsArray.Count > 0)
                                {
                                    foreach (var detail in detailsArray)
                                    {
                                        string detailMessage = detail["Message"]?.ToString() ?? "";
                                        if (!string.IsNullOrEmpty(detailMessage) &&
                                            (detailMessage.Contains("host runtime", StringComparison.OrdinalIgnoreCase) ||
                                            detailMessage.Contains("InternalServerError", StringComparison.OrdinalIgnoreCase)))
                                        {
                                            _logger.LogInternalInformation("Found host runtime error in statusMessage details: {message}", detailMessage);
                                            return true;
                                        }
                                    }
                                }

                                // Check if the StatusMessage contains host runtime related errors
                                if (!string.IsNullOrEmpty(statusMessageContent) &&
                                    (statusMessageContent.Contains("host runtime", StringComparison.OrdinalIgnoreCase) ||
                                    statusMessageContent.Contains("InternalServerError", StringComparison.OrdinalIgnoreCase)))
                                {
                                    _logger.LogInternalInformation("Found host runtime error in statusMessage: {message}", statusMessageContent);
                                    return true;
                                }
                            }
                            catch (Exception ex)
                            {
                                // If there's an error parsing StatusMessage as JSON, log it but continue checking
                                _logger.LogInternalWarning(ex, "Error parsing StatusMessage as JSON. Will check as regular string.");

                                string statusMessageString = eventItem["StatusMessage"]?.ToString() ?? string.Empty;
                                if (statusMessageString.Contains("host runtime", StringComparison.OrdinalIgnoreCase) ||
                                    statusMessageString.Contains("InternalServerError", StringComparison.OrdinalIgnoreCase))
                                {
                                    _logger.LogInternalInformation("Found host runtime error in statusMessage string: {message}", statusMessageString);
                                    return true;
                                }
                            }
                        }

                        // Continue with the existing checks for errorMessage and operation
                        if ((errorMessage.Contains("host runtime", StringComparison.OrdinalIgnoreCase) ||
                             operation.Contains("host", StringComparison.OrdinalIgnoreCase)) &&
                            (errorMessage.Contains("InternalServerError", StringComparison.OrdinalIgnoreCase) ||
                             errorMessage.Contains("Error", StringComparison.OrdinalIgnoreCase)))
                        {
                            _logger.LogInternalInformation("Found host runtime error in errorMessage: {message}", errorMessage);
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error checking for host runtime errors: {resourceId}", resourceId);
                return false;
            }
        }

        [KernelFunction("trigger_function_app_sync")]
        [Description("This function triggers a sync operation on a Function App's host to check for runtime errors or refresh the function app")]
        public async Task<string> TriggerFunctionAppSync(string resourceId)
        {
            _logger.LogInternalInformation("[trigger_function_app_sync] Invoked with resourceId {resourceId}", resourceId);
            if (string.IsNullOrEmpty(resourceId))
            {
                _logger.LogInternalError("Resource ID is null or empty.");
                return "Invalid resource ID.";
            }

            try
            {
                // Call ArmHelper's SyncFunctionAppHost method
                _logger.LogInternalInformation("Triggering Function App host sync for {resourceId}", resourceId);
                string syncResponse = await _armHelper.SyncFunctionAppHost(resourceId);

                return syncResponse;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error triggering Function App host sync for {resourceId}", resourceId);
                return $"Failed to sync Function App host: {ex.Message}";
            }
        }
    }
}
