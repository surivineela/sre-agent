// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using Agent.Graph.Schema;

namespace Agent.Plugins.Interface
{
    /// <summary>
    /// Interface for Azure Activity Logs and Change History operations.
    /// Provides methods for analyzing activity logs, deployment failures, and change history.
    /// </summary>
    public interface IAzureActivityLogsPlugin
    {
        /// <summary>
        /// The current thread context for the plugin, used to identify the conversation thread when sending messages or images.
        /// </summary>
        Guid? ThreadId { get; set; }

        /// <summary>
        /// Retrieves and summarizes activity logs for a resource and its dependent resources.
        /// Analyzes recent operations, changes, and potential issues over a specified time period.
        /// </summary>
        /// <param name="resourceId">Azure Resource Id of the resource to analyze.</param>
        /// <param name="hoursBack">Number of hours of logs to retrieve and analyze. Default is 24.</param>
        /// <param name="threadId">Optional threadId for the current conversation.</param>
        /// <returns>A summary of the activity logs with key insights and potential issues.</returns>
        Task<string> FetchAndSummarizeActivityLogs(string resourceId, int hoursBack = 24, Guid? threadId = null);

        /// <summary>
        /// Fetches activity logs and components for a specific resource. 
        /// </summary>
        /// <param name="resourceId"></param>
        /// <param name="daysBack"></param>
        /// <param name="threadId"></param>
        /// <returns></returns>
        Task<(List<Dictionary<string, object>> ActivityLogs, List<Node> Components)> FetchActivityLogsAndComponents(string resourceId, int daysBack = 1, Guid? threadId = null);

        /// <summary>
        /// Analyzes Azure deployment failures and provides detailed error information.
        /// </summary>
        /// <param name="resourceId">Azure Resource Id of the resource to analyze deployment failures for.</param>
        /// <param name="hoursBack">Number of hours to look back for deployment failures. Default is 24.</param>
        /// <param name="threadId">Optional threadId for the current conversation.</param>
        /// <returns>A detailed analysis of deployment failures with troubleshooting insights.</returns>
        Task<string> AnalyzeDeploymentFailures(string resourceId, int hoursBack = 24, Guid? threadId = null);

        /// <summary>
        /// Retrieves detailed change history for a specific activity log entry using correlation ID.
        /// Provides comprehensive information about what changes were made, including before/after states,
        /// deployment details, and resource modifications.
        /// </summary>
        /// <param name="correlationId">Correlation ID from the activity log entry to get detailed change history for.</param>
        /// <param name="resourceId">Azure Resource Id of the resource that was changed.</param>
        /// <param name="threadId">Optional threadId for the current conversation.</param>
        /// <returns>Detailed change history including what was changed, who made the change, and the impact.</returns>
        Task<string> GetChangeHistory(string correlationId, string resourceId, Guid? threadId = null);

        /// <summary>
        /// Displays a visual change diff viewer for detailed change analysis between before and after states.
        /// Shows property-level changes with highlighting of additions, deletions, and modifications.
        /// </summary>
        /// <param name="correlationId">Correlation ID from the activity log entry to show change diff for.</param>
        /// <param name="resourceId">Azure Resource Id of the resource that was changed.</param>
        /// <param name="title">Title to display for the diff viewer.</param>
        /// <param name="description">Description of the changes being shown.</param>
        /// <param name="threadId">Optional threadId for the current conversation.</param>
        /// <returns>Success message indicating the diff viewer was displayed.</returns>
        Task<string> ShowChangeDiffViewer(string correlationId, string resourceId, string title, string description, Guid? threadId = null);
    }
}
