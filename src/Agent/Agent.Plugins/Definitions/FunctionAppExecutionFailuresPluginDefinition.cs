// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Attributes;
using Agent.Core.Models;
using Agent.Core.Models.Charts;
using Agent.Framework;
using Agent.Plugins.Interface;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(Category = ToolCategories.Diagnostics)]
    public class FunctionAppExecutionFailuresPluginDefinition(IFunctionAppExecutionFailuresPlugin functionAppExecutionFailuresPlugin)
    {
        private readonly IFunctionAppExecutionFailuresPlugin _functionAppExecutionFailuresPlugin = functionAppExecutionFailuresPlugin;

        [Description("Gets a summary of execution failures for an Azure Function App. Do not call for FlexConsumption SKU")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> GetFunctionAppExecutionFailures(
            [Description("The full Azure resource ID of the Function App to analyze")] string resourceId)
        {
            return await _functionAppExecutionFailuresPlugin.GetFunctionAppExecutionFailures(resourceId);
        }

        [Description("Gets call stack information for Azure Function App executions")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> GetFunctionAppCallStacks(
            [Description("The full Azure resource ID of the Function App to analyze")] string resourceId)
        {
            return await _functionAppExecutionFailuresPlugin.GetFunctionAppCallStacks(resourceId);
        }

        [Description("Gets a summary of failed invocations grouped by function for an Azure Function App")]
        [AgentTool(ToolMode.Auto)]
        public async Task<IReadOnlyList<FailedRequestsTimeSeriesData>> GetFailedFunctionInvocations(
            [Description("The full Azure resource ID of the Function App to analyze")] string resourceId,
            [Description("Optional duration in minutes to query for (defaults to 60 minutes)")] int? minutes = null)
        {
            return await _functionAppExecutionFailuresPlugin.GetFailedFunctionInvocations(resourceId, minutes);
        }

        [Description("Gets the top 3 exceptions grouped by function for an Azure Function App")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> GetTop3ExceptionsPerFunction(
            [Description("The full Azure resource ID of the Function App to analyze")] string resourceId,
            [Description("Optional start time for the query (defaults to 1 hour ago)")] DateTime? startTime = null,
            [Description("Optional end time for the query (defaults to current time minus 15 minutes)")] DateTime? endTime = null)
        {
            return await _functionAppExecutionFailuresPlugin.GetTop3ExceptionsPerFunction(resourceId, startTime, endTime);
        }

        [Description("Gets the top 3 exceptions with detailed stack traces and exception messages for an Azure Function App")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> GetTop3ExceptionsWithStackTraces(
            [Description("The full Azure resource ID of the Function App to analyze")] string resourceId,
            [Description("Optional start time for the query (defaults to 1 hour ago)")] DateTime? startTime = null,
            [Description("Optional end time for the query (defaults to current time minus 15 minutes)")] DateTime? endTime = null)
        {
            return await _functionAppExecutionFailuresPlugin.GetTop3ExceptionsWithStackTraces(resourceId, startTime, endTime);
        }

        [Description("Gets host runtime error events from the activity logs for an Azure Function App")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> GetHostRuntimeErrorEvents(
            [Description("The full Azure resource ID of the Function App to analyze")] string resourceId,
            [Description("Optional start time for the query (defaults to 3 hours ago)")] DateTime? startTime = null,
            [Description("Optional end time for the query (defaults to current time)")] DateTime? endTime = null)
        {
            return await _functionAppExecutionFailuresPlugin.GetHostRuntimeErrorEvents(resourceId, startTime, endTime);
        }

        [Description("Checks if a resource is a Function App by verifying its 'kind' property contains 'functionapp'")]
        [AgentTool(ToolMode.Auto)]
        public async Task<bool> IsFunctionApp(
            [Description("The full Azure resource ID to check")] string resourceId)
        {
            return await _functionAppExecutionFailuresPlugin.IsFunctionApp(resourceId);
        }

        [Description("Checks if a Function App has host runtime related errors in its activity logs")]
        [AgentTool(ToolMode.Auto)]
        public async Task<bool> HasHostRuntimeErrors(
            [Description("The full Azure resource ID of the Function App to check")] string resourceId,
            [Description("Optional start time for the query (defaults to 1 hour ago)")] DateTime? startTime = null,
            [Description("Optional end time for the query (defaults to current time minus 15 minutes)")] DateTime? endTime = null)
        {
            return await _functionAppExecutionFailuresPlugin.HasHostRuntimeErrors(resourceId, startTime, endTime);
        }

        [Description("Triggers a sync operation on a Function App's host to check for runtime errors or refresh the function app")]
        [RequiresApproval]
        [WriteAction]
        public async Task<string> TriggerFunctionAppSync(
            [Description("The full Azure resource ID of the Function App to sync")] string resourceId)
        {
            return await _functionAppExecutionFailuresPlugin.TriggerFunctionAppSync(resourceId);
        }
    }
}
