using System.ComponentModel;
using Agent.Core.Attributes;
using Agent.Framework;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions
{

    /// <summary>
    /// Definition for the Function App Deployment Checks Plugin
    /// </summary>
    [AgentToolPlugin]
    public class FunctionAppDeploymentChecksPluginDefinition
    {
        private readonly IFunctionAppDeploymentChecksPlugin _functionAppDeploymentChecksPlugin;

        /// <summary>
        /// Constructor for FunctionAppDeploymentChecksPluginDefinition
        /// </summary>
        /// <param name="functionAppDeploymentChecksPlugin">The Function App Deployment Checks Plugin implementation</param>
        public FunctionAppDeploymentChecksPluginDefinition(IFunctionAppDeploymentChecksPlugin functionAppDeploymentChecksPlugin)
        {
            _functionAppDeploymentChecksPlugin = functionAppDeploymentChecksPlugin;
        }

        /// <summary>
        /// Gets Function App deployment information for a Function App
        /// </summary>
        [Description("Gets Function App deployment information to identify potential deployment issues. " +
                    "Analyzes deployment history, source control information, deployment methods, and other deployment-related metrics. " +
                    "Returns detailed analysis with potential deployment issues and recommendations.")]
        public async Task<string> GetFunctionAppDeploymentChecks(
            [Description("The full Azure resource ID of the Function App to check deployments for.")] string resourceId,
            [Description("Optional start time for the query (defaults to 1 hour ago)")] DateTime? startTime = null,
            [Description("Optional end time for the query (defaults to current time minus 15 minutes)")] DateTime? endTime = null)
        {
            return await _functionAppDeploymentChecksPlugin.GetFunctionAppDeploymentChecks(resourceId, startTime, endTime);
        }

        /// <summary>
        /// Gets Function App deployment history for a Function App
        /// </summary>
        [Description("Gets detailed Function App deployment history to track all deployment activities. " +
                    "Retrieves chronological deployment records, including deployment source, trigger, status, and timestamps. " +
                    "Returns comprehensive deployment timeline with success/failure information.")]
        public async Task<string> GetFunctionAppDeploymentHistory(
            [Description("The full Azure resource ID of the Function App to retrieve deployment history for.")] string resourceId,
            [Description("Optional start time for the query (defaults to 1 hour ago)")] DateTime? startTime = null,
            [Description("Optional end time for the query (defaults to current time minus 15 minutes)")] DateTime? endTime = null)
        {
            return await _functionAppDeploymentChecksPlugin.GetFunctionAppDeploymentHistory(resourceId, startTime, endTime);
        }

        /// <summary>
        /// Gets Function App slot swap information for a Function App
        /// </summary>
        [Description("Gets detailed Function App slot swap information to analyze swap operations. " +
                    "Retrieves history of slot swaps including timestamp, source and target slots, and status. " +
                    "Returns comprehensive history of swap operations to troubleshoot deployment and availability issues.")]
        public async Task<string> GetFunctionAppSlotSwapHistory(
            [Description("The full Azure resource ID of the Function App to retrieve slot swap history for.")] string resourceId,
            [Description("Optional start time for the query (defaults to 1 hour ago)")] DateTime? startTime = null,
            [Description("Optional end time for the query (defaults to current time minus 15 minutes)")] DateTime? endTime = null)
        {
            return await _functionAppDeploymentChecksPlugin.GetFunctionAppSlotSwapHistory(resourceId, startTime, endTime);
        }
        
        /// <summary>
        /// Gets detailed deployment failure analysis for a Function App
        /// </summary>
        [Description("Gets in-depth analysis of deployment failures for Windows Function Apps. " +
                    "Analyzes deployment logs, identifies common failure patterns, and provides detailed diagnostics. " +
                    "Returns comprehensive deployment failure analysis with root cause identification and suggested remediation steps. " +
                    "Note: This tool only works for Windows Function Apps.")]
        public async Task<string> GetFunctionAppDeploymentFailureAnalysis(
            [Description("The full Azure resource ID of the Windows Function App to analyze deployment failures for.")] string resourceId,
            [Description("Optional start time for the query (defaults to 1 hour ago)")] DateTime? startTime = null,
            [Description("Optional end time for the query (defaults to current time minus 15 minutes)")] DateTime? endTime = null)
        {
            return await _functionAppDeploymentChecksPlugin.GetFunctionAppDeploymentFailureAnalysis(resourceId, startTime, endTime);
        }

        /// <summary>
        /// Verifies if a zip file exists in Azure Storage
        /// </summary>
        [Description("Verifies if a zip file exists in Azure Storage. " +
                    "Checks if the specified zip file is accessible from its URL location. " +
                    "If no zip file path is provided, retrieves the path from the WEBSITE_RUN_FROM_PACKAGE app setting. " +
                    "Returns verification status, path information, and details about the file if it exists.")]
        public async Task<Models.ZipFileVerificationResult> VerifyZipFileExistsAsync(
            [Description("The full Azure resource ID of the Function App or Web App to verify.")] string resourceId,
            [Description("Optional path to the zip file. If not provided, the WEBSITE_RUN_FROM_PACKAGE app setting will be used.")] string zipFilePath = null)
        {
            return await _functionAppDeploymentChecksPlugin.VerifyZipFileExistsAsync(resourceId, zipFilePath);
        }

        /// <summary>
        /// Updates the WEBSITE_RUN_FROM_PACKAGE app setting to point to a new zip file
        /// </summary>
        [Description("Updates the WEBSITE_RUN_FROM_PACKAGE app setting to point to a new zip file in Azure Storage. " +
                    "Validates the provided zip file path, verifies that the file exists in Azure Storage, " +
                    "renames the existing WEBSITE_RUN_FROM_PACKAGE to SREAGENT_RENAMED_WEBSITE_RUN_FROM_PACKAGE, " +
                    "and creates a new WEBSITE_RUN_FROM_PACKAGE with the provided value. " +
                    "Returns details about the update operation including success status and error information if applicable.")]
        [RequiresApproval]
        public async Task<Models.WebsiteRunFromPackageUpdateResult> UpdateWebsiteRunFromPackageAsync(
            [Description("The full Azure resource ID of the Function App or Web App to update.")] string resourceId,
            [Description("The path to the zip file in Azure Storage. Must be a valid URL to a zip file in an Azure Storage Blob container.")] string zipFilePath)
        {
            return await _functionAppDeploymentChecksPlugin.UpdateWebsiteRunFromPackageAsync(resourceId, zipFilePath);
        }
    }
}
