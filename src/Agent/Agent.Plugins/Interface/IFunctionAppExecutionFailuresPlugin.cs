// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Charts;

namespace Agent.Plugins.Interface
{
    public interface IFunctionAppExecutionFailuresPlugin
    {
        /// <summary>
        /// Gets execution failures for a Function App
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App</param>
        /// <returns>A summary of function execution failures</returns>
        Task<string> GetFunctionAppExecutionFailures(string resourceId);

        /// <summary>
        /// Gets call stacks for functions in the Function App
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App</param>
        /// <returns>Call stack information for functions in the app</returns>
        Task<string> GetFunctionAppCallStacks(string resourceId);

        /// <summary>
        /// Gets failed function invocations for a specified time range
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App</param>
        /// <param name="minutes">Optional duration in minutes to query for (defaults to 60 minutes)</param>
        /// <returns>A collection of time series data points showing failed invocations by function</returns>
        Task<IReadOnlyList<FailedRequestsTimeSeriesData>> GetFailedFunctionInvocations(string resourceId, int? minutes = null);

        /// <summary>
        /// Gets top 3 exceptions per function with optional time range
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App</param>
        /// <param name="startTime">Optional start time for the query (defaults to 1 hour ago)</param>
        /// <param name="endTime">Optional end time for the query (defaults to current time minus 15 minutes)</param>
        /// <returns>The top 3 exceptions grouped by function</returns>
        Task<string> GetTop3ExceptionsPerFunction(string resourceId, DateTime? startTime = null, DateTime? endTime = null);

        /// <summary>
        /// Gets top 3 exceptions with detailed stack traces and exception messages
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App</param>
        /// <param name="startTime">Optional start time for the query (defaults to 1 hour ago)</param>
        /// <param name="endTime">Optional end time for the query (defaults to current time minus 15 minutes)</param>
        /// <returns>The top 3 exceptions with detailed stack traces and messages</returns>
        Task<string> GetTop3ExceptionsWithStackTraces(string resourceId, DateTime? startTime = null, DateTime? endTime = null);

        /// <summary>
        /// Gets host runtime error events from the activity logs
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App</param>
        /// <param name="startTime">Optional start time for the query (defaults to 3 hours ago)</param>
        /// <param name="endTime">Optional end time for the query (defaults to current time)</param>
        /// <returns>A summary of host runtime error events</returns>
        Task<string> GetHostRuntimeErrorEvents(string resourceId, DateTime? startTime = null, DateTime? endTime = null);

        /// <summary>
        /// Checks if a resource is a Function App by verifying its 'kind' property contains 'functionapp'
        /// </summary>
        /// <param name="resourceId">The Azure resource ID to check</param>
        /// <returns>True if the resource is a Function App, false otherwise</returns>
        Task<bool> IsFunctionApp(string resourceId);

        /// <summary>
        /// Checks if a Function App has host runtime related errors in its activity logs
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App</param>
        /// <param name="startTime">Optional start time for the query (defaults to 1 hour ago)</param>
        /// <param name="endTime">Optional end time for the query (defaults to current time minus 15 minutes)</param>
        /// <returns>True if host runtime errors are detected, false otherwise</returns>
        Task<bool> HasHostRuntimeErrors(string resourceId, DateTime? startTime = null, DateTime? endTime = null);

        /// <summary>
        /// Triggers a sync operation on a Function App's host to check for runtime errors or refresh the function app
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App to sync</param>
        /// <returns>The response from the sync operation</returns>
        Task<string> TriggerFunctionAppSync(string resourceId);
    }
}
