// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace Agent.Plugins.Definitions
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
        /// Gets failed requests per function with optional time range
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App</param>
        /// <param name="startTime">Optional start time for the query (defaults to 1 hour ago)</param>
        /// <param name="endTime">Optional end time for the query (defaults to current time minus 15 minutes)</param>
        /// <returns>A summary of failed requests grouped by function</returns>
        Task<string> GetFailedRequestsPerFunction(string resourceId, DateTime? startTime = null, DateTime? endTime = null);

        /// <summary>
        /// Gets top 3 exceptions per function with optional time range
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App</param>
        /// <param name="startTime">Optional start time for the query (defaults to 1 hour ago)</param>
        /// <param name="endTime">Optional end time for the query (defaults to current time minus 15 minutes)</param>
        /// <returns>The top 3 exceptions grouped by function</returns>
        Task<string> GetTop3ExceptionsPerFunction(string resourceId, DateTime? startTime = null, DateTime? endTime = null);

        /// <summary>
        /// Gets host runtime error events from the activity logs
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App</param>
        /// <param name="startTime">Optional start time for the query (defaults to 3 hours ago)</param>
        /// <param name="endTime">Optional end time for the query (defaults to current time)</param>
        /// <returns>A summary of host runtime error events</returns>
        Task<string> GetHostRuntimeErrorEvents(string resourceId, DateTime? startTime = null, DateTime? endTime = null);
    }
}
