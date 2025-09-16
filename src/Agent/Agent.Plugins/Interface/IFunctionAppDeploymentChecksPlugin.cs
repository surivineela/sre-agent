using Agent.Plugins.Models;

namespace Agent.Plugins.Interface;

/// <summary>
/// Plugin for diagnosing and analyzing Function App deployment information
/// </summary>
public interface IFunctionAppDeploymentChecksPlugin
{
    /// <summary>
    /// Gets the thread ID for the plugin
    /// </summary>
    Guid? ThreadId { get; set; }

    /// <summary>
    /// Gets Function App deployment information for a Function App
    /// </summary>
    /// <param name="resourceId">The Azure resource ID of the Function App</param>
    /// <param name="startTime">Optional start time for the query (defaults to 1 hour ago)</param>
    /// <param name="endTime">Optional end time for the query (defaults to current time minus 15 minutes)</param>
    /// <returns>A summary of function app deployment information</returns>
    Task<string> GetFunctionAppDeploymentChecks(string resourceId, DateTime? startTime = null, DateTime? endTime = null);

    /// <summary>
    /// Gets Function App deployment history for a Function App
    /// </summary>
    /// <param name="resourceId">The Azure resource ID of the Function App</param>
    /// <param name="startTime">Optional start time for the query (defaults to 1 hour ago)</param>
    /// <param name="endTime">Optional end time for the query (defaults to current time minus 15 minutes)</param>
    /// <returns>A detailed history of function app deployments</returns>
    Task<string> GetFunctionAppDeploymentHistory(string resourceId, DateTime? startTime = null, DateTime? endTime = null);

    /// <summary>
    /// Gets Function App slot swap information for a Function App
    /// </summary>
    /// <param name="resourceId">The Azure resource ID of the Function App</param>
    /// <param name="startTime">Optional start time for the query (defaults to 1 hour ago)</param>
    /// <param name="endTime">Optional end time for the query (defaults to current time minus 15 minutes)</param>
    /// <returns>A detailed history of function app slot swap operations</returns>
    Task<string> GetFunctionAppSlotSwapHistory(string resourceId, DateTime? startTime = null, DateTime? endTime = null);

    /// <summary>
    /// Gets detailed deployment failure analysis for a Function App
    /// Note: This tool only works for Windows SKUs
    /// </summary>
    /// <param name="resourceId">The Azure resource ID of the Function App</param>
    /// <param name="startTime">Optional start time for the query (defaults to 1 hour ago)</param>
    /// <param name="endTime">Optional end time for the query (defaults to current time minus 15 minutes)</param>
    /// <returns>A detailed analysis of deployment failures and potential causes</returns>
    Task<string> GetFunctionAppDeploymentFailureAnalysis(string resourceId, DateTime? startTime = null, DateTime? endTime = null);

    /// <summary>
    /// Updates the WEBSITE_RUN_FROM_PACKAGE app setting to a new Azure Storage zip file path
    /// </summary>
    /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
    /// <param name="zipFilePath">Path to the zip file in Azure Storage</param>
    /// <returns>A result indicating the success or failure of the update operation</returns>
    Task<Models.WebsiteRunFromPackageUpdateResult> UpdateWebsiteRunFromPackageAsync(string resourceId, string zipFilePath);

    /// <summary>
    /// Lists blobs in a storage container using ARM REST API
    /// </summary>
    /// <param name="containerUri">The URI of the container to list blobs from, including any query parameters</param>
    /// <returns>A result containing the list of blobs in the container</returns>
    Task<Models.StorageBlobListResult> ListStorageBlobsAsync(string containerUri);


    /// <summary>
    /// Checks if the Function App has WEBSITE_RUN_FROM_PACKAGE configuration issues
    /// </summary>
    /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
    /// <returns>True if there are WEBSITE_RUN_FROM_PACKAGE issues that require specialized handling</returns>
    Task<bool> HasRunFromPackageIssueAsync(string resourceId);
}
