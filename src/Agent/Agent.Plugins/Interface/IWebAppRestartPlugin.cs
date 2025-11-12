// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Charts;

namespace Agent.Plugins.Interface
{
    public interface IWebAppRestartPlugin
    {
        /// <summary>
        /// Gets web app restart execution data
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Web App</param>
        /// <returns>A summary of web app restart execution data</returns>
        Task<string> GetWebAppRestartExecution(string resourceId);

        /// <summary>
        /// Gets call stacks for a web app
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Web App</param>
        /// <returns>Call stack information for the web app</returns>
        Task<string> GetWebAppCallStacks(string resourceId);

        /// <summary>
        /// Gets failed request invocations for a specified time range
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Web App</param>
        /// <param name="minutes">Optional duration in minutes to query for (defaults to 90 minutes)</param>
        /// <returns>A collection of time series data points showing failed invocations</returns>
        Task<IReadOnlyList<FailedRequestsTimeSeriesData>> GetFailedRequestInvocations(string resourceId, int? minutes = null);

        /// <summary>
        /// Gets top 3 exceptions with optional time range
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Web App</param>
        /// <param name="startTime">Optional start time for the query (defaults to 1 hour ago)</param>
        /// <param name="endTime">Optional end time for the query (defaults to current time minus 15 minutes)</param>
        /// <returns>The top 3 exceptions</returns>
        Task<string> GetTop3Exceptions(string resourceId, DateTime? startTime = null, DateTime? endTime = null);        /// <summary>
                                                                                                                        /// Checks if a resource is a Web App by verifying its 'kind' property
                                                                                                                        /// </summary>
                                                                                                                        /// <param name="resourceId">The Azure resource ID to check</param>
                                                                                                                        /// <returns>True if the resource is a Web App, false otherwise</returns>
        Task<bool> IsWebApp(string resourceId);
    }
}
