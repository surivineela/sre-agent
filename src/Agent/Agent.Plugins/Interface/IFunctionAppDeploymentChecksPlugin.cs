using System;
using System.Threading.Tasks;

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
    /// Verifies if a zip file exists in Azure Storage
    /// </summary>
    /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
    /// <param name="zipFilePath">Optional path to the zip file. If not provided, the WEBSITE_RUN_FROM_PACKAGE app setting will be used</param>
    /// <returns>A verification result indicating if the zip file exists and details about the verification</returns>
    Task<Models.ZipFileVerificationResult> VerifyZipFileExistsAsync(string resourceId, string zipFilePath = null);

    /// <summary>
    /// Updates the WEBSITE_RUN_FROM_PACKAGE app setting to a new Azure Storage zip file path
    /// </summary>
    /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
    /// <param name="zipFilePath">Path to the zip file in Azure Storage</param>
    /// <returns>A result indicating the success or failure of the update operation</returns>
    Task<Models.WebsiteRunFromPackageUpdateResult> UpdateWebsiteRunFromPackageAsync(string resourceId, string zipFilePath);
}
