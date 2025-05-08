// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Agent.Core.Helpers;
using Agent.Logging;
using Agent.Plugins.Definitions;
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
                // Note: You'll need to provide an appropriate detectorId below
                var result = await _armHelper.GetDetectorResponseWithTime(resourceId, "functionExecutionErrors");

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

        public async Task<string> GetFailedRequestsPerFunction(string resourceId, DateTime? startTime = null, DateTime? endTime = null)
        {
            // Default to past hour if not specified, with endTime being now minus 15 minutes
            startTime ??= DateTime.UtcNow.AddHours(-1);
            endTime ??= DateTime.UtcNow.AddMinutes(-15);

            _logger.LogInternalInformation("[GetFailedRequestsPerFunction] Invoked with resourceId {resourceId}, startTime {startTime}, endTime {endTime}", resourceId, startTime, endTime);

            string resourceName = resourceId;
            if (resourceId.Contains('/'))
            {
                var splitResourceParts = resourceId.Split('/');
                resourceName = splitResourceParts[splitResourceParts.Length - 1];
            }

            string failedRequestsPerFunctionQuery = $@"
                    let start=datetime({startTime.Value:O});
                    let end=datetime({endTime.Value:O});
                    let timeGrain=5m;
                    let dataset=requests
                        | where timestamp > start and timestamp < end
                        | where cloud_RoleName =~ ""{resourceName}"" or cloud_RoleName startswith ""{resourceName}-""
                        | where client_Type != ""Browser"";
                    dataset
                    | summarize FailedCount=sumif(itemCount, success == false) by bin(timestamp, timeGrain)";

            var failedRequestsPerFunction = await _appInsightsPlugin.ExecuteAppInsightsQuery(resourceId, failedRequestsPerFunctionQuery);
            return failedRequestsPerFunction;
        }

        public async Task<string> GetTop3ExceptionsPerFunction(string resourceId, DateTime? startTime = null, DateTime? endTime = null)
        {
            // Default to past hour if not specified, with endTime being now minus 15 minutes
            startTime ??= DateTime.UtcNow.AddHours(-1);
            endTime ??= DateTime.UtcNow.AddMinutes(-15);

            _logger.LogInternalInformation("[GetTop3ExceptionsPerFunction] Invoked with resourceId {resourceId}, startTime {startTime}, endTime {endTime}", resourceId, startTime, endTime);

            string top3ExceptionsPerFunctionQuery = $@"
                    let start=datetime({startTime.Value:O});
                    let end=datetime({endTime.Value:O});
                    let timeGrain=5m;
                    let dataset=exceptions
                        | where timestamp > start and timestamp < end
                        | where client_Type != ""Browser""
                        | extend FunctionName = iif(outerMessage has ""Result: Function"", extract(@""Result: Function '([^']+)'"", 1, outerMessage), """")
                        | extend FunctionName = iif(isempty(FunctionName), iif(method has "".Run"", extract(@""([^.]+).([^.]+).Run"", 2, method), method), FunctionName)
                        | extend FunctionName = iif(isempty(FunctionName),method,FunctionName);
                    dataset
                        | summarize _count=sum(itemCount) by type, FunctionName
                        | sort by _count desc
                        | top 3 by _count";

            var top3ExceptionsPerFunction = await _appInsightsPlugin.ExecuteAppInsightsQuery(resourceId, top3ExceptionsPerFunctionQuery);
            return top3ExceptionsPerFunction;
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
                string formattedStartTime = startTime.HasValue ? startTime.Value.ToString("yyyy-MM-ddTHH:mm:ssZ") : null;
                string formattedEndTime = endTime.HasValue ? endTime.Value.ToString("yyyy-MM-ddTHH:mm:ssZ") : null;

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
                        if (eventItem["StatusMessage"] != null && !string.IsNullOrEmpty(eventItem["StatusMessage"].ToString()))
                        {
                            try
                            {
                                var statusMessageString = eventItem["StatusMessage"].ToString();
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

                                string statusMessageString = eventItem["StatusMessage"].ToString();
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
    }
}
