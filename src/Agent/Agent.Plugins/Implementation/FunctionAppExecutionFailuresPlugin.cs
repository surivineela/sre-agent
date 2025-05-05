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

namespace Agent.Plugins.Implementation
{
    public class FunctionAppExecutionFailuresPlugin : IFunctionAppExecutionFailuresPlugin
    {
        private readonly IArmPlugin _armPlugin;
        private readonly ArmHelper _armHelper;
        private readonly IAppCodeAnalysisPlugin _appCodeAnalysisPlugin;
        private readonly IAppInsightsPlugin _appInsightsPlugin;
        private readonly ILogger<FunctionAppExecutionFailuresPlugin> _logger;

        public FunctionAppExecutionFailuresPlugin(
            IArmPlugin armPlugin,
            ArmHelper armHelper,
            IAppCodeAnalysisPlugin appCodeAnalysisPlugin,
            IAppInsightsPlugin appInsightsPlugin,
            ILogger<FunctionAppExecutionFailuresPlugin> logger)
        {
            _armPlugin = armPlugin;
            _armHelper = armHelper;
            _appCodeAnalysisPlugin = appCodeAnalysisPlugin;
            _appInsightsPlugin = appInsightsPlugin;
            _logger = logger;
        }

        public async Task<string> GetFunctionAppExecutionFailures(string resourceId)
        {
            _logger.LogInternalInformation($"[get_function_app_execution_failures] Invoked with resourceId: {resourceId}");
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
                _logger.LogInternalError(ex, $"Error getting function app execution failures for {resourceId}");
                return $"Failed to retrieve execution failures: {ex.Message}";
            }
        }

        public async Task<string> GetFunctionAppCallStacks(string resourceId)
        {
            _logger.LogInternalInformation($"[get_function_app_call_stacks] Invoked with resourceId: {resourceId}");
            
            try
            {
                // Call AppCodeAnalysisPlugin's GetCallStackForApp
                var callStacks = await _appCodeAnalysisPlugin.GetCallStackForApp(resourceId);
                
                return callStacks;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error getting function app call stacks for {resourceId}");
                return $"Failed to retrieve call stacks: {ex.Message}";
            }
        }

        public async Task<string> GetFailedRequestsPerFunction(string resourceId, DateTime? startTime = null, DateTime? endTime = null)
        {
            // Default to past hour if not specified, with endTime being now minus 15 minutes
            startTime ??= DateTime.UtcNow.AddHours(-1);
            endTime ??= DateTime.UtcNow.AddMinutes(-15);

            _logger.LogInternalInformation($"[GetFailedRequestsPerFunction] Invoked with resourceId: {resourceId}, startTime: {startTime}, endTime: {endTime}");

            string resourceName = resourceId;
            if (resourceId.Contains('/'))
            {
                var splitResourceParts = resourceId.Split('/');
                resourceName = splitResourceParts[splitResourceParts.Length - 1];
            }
            
            string failedRequestsPerFunctionQuery = $@"
                let start=datetime({startTime.Value.ToString("O")});
                let end=datetime({endTime.Value.ToString("O")});
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

            _logger.LogInternalInformation($"[GetTop3ExceptionsPerFunction] Invoked with resourceId: {resourceId}, startTime: {startTime}, endTime: {endTime}");

            string top3ExceptionsPerFunctionQuery = $@"
                let start=datetime({startTime.Value.ToString("O")});
                let end=datetime({endTime.Value.ToString("O")});
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
            _logger.LogInternalInformation($"[get_host_runtime_error_events] Invoked with resourceId: {resourceId}");
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
                        Timestamp = e.Timestamp,
                        Operation = e.OperationName,
                        ErrorMessage = e.ErrorMessage,
                        Status = e.Status,
                        Caller = e.Caller,
                        ResourceId = e.ResourceId,
                        IsSuccessful = e.IsSuccessful
                    }).ToList()
                };
                
                return Newtonsoft.Json.JsonConvert.SerializeObject(response, Newtonsoft.Json.Formatting.Indented);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error getting error events for {resourceId}");
                return $"Failed to retrieve error events: {ex.Message}";
            }
        }
    }
}
