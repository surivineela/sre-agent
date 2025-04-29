// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Definitions
{
    public class FunctionAppExecutionFailuresPluginDefinition
    {
        private readonly IFunctionAppExecutionFailuresPlugin _functionAppExecutionFailuresPlugin;

        public FunctionAppExecutionFailuresPluginDefinition(IFunctionAppExecutionFailuresPlugin functionAppExecutionFailuresPlugin)
        {
            _functionAppExecutionFailuresPlugin = functionAppExecutionFailuresPlugin;
        }

        [KernelFunction("get_function_app_execution_failures")]
        [Description("Gets a summary of execution failures for an Azure Function App")]
        public async Task<string> GetFunctionAppExecutionFailures(
            [Description("The full Azure resource ID of the Function App to analyze")] string resourceId)
        {
            return await _functionAppExecutionFailuresPlugin.GetFunctionAppExecutionFailures(resourceId);
        }

        [KernelFunction("get_function_app_call_stacks")]
        [Description("Gets call stack information for Azure Function App executions")]
        public async Task<string> GetFunctionAppCallStacks(
            [Description("The full Azure resource ID of the Function App to analyze")] string resourceId)
        {
            return await _functionAppExecutionFailuresPlugin.GetFunctionAppCallStacks(resourceId);
        }

        [KernelFunction("get_failed_requests_per_function")]
        [Description("Gets a summary of failed requests grouped by function for an Azure Function App")]
        public async Task<string> GetFailedRequestsPerFunction(
            [Description("The full Azure resource ID of the Function App to analyze")] string resourceId,
            [Description("Optional start time for the query (defaults to 1 hour ago)")] DateTime? startTime = null,
            [Description("Optional end time for the query (defaults to current time minus 15 minutes)")] DateTime? endTime = null)
        {
            return await _functionAppExecutionFailuresPlugin.GetFailedRequestsPerFunction(resourceId, startTime, endTime);
        }

        [KernelFunction("get_top3_exceptions_per_function")]
        [Description("Gets the top 3 exceptions grouped by function for an Azure Function App")]
        public async Task<string> GetTop3ExceptionsPerFunction(
            [Description("The full Azure resource ID of the Function App to analyze")] string resourceId,
            [Description("Optional start time for the query (defaults to 1 hour ago)")] DateTime? startTime = null,
            [Description("Optional end time for the query (defaults to current time minus 15 minutes)")] DateTime? endTime = null)
        {
            return await _functionAppExecutionFailuresPlugin.GetTop3ExceptionsPerFunction(resourceId, startTime, endTime);
        }

        [KernelFunction("get_host_runtime_error_events")]
        [Description("Gets host runtime error events from the activity logs for an Azure Function App")]
        public async Task<string> GetHostRuntimeErrorEvents(
            [Description("The full Azure resource ID of the Function App to analyze")] string resourceId,
            [Description("Optional start time for the query (defaults to 3 hours ago)")] DateTime? startTime = null,
            [Description("Optional end time for the query (defaults to current time)")] DateTime? endTime = null)
        {
            return await _functionAppExecutionFailuresPlugin.GetHostRuntimeErrorEvents(resourceId, startTime, endTime);
        }
    }
}
