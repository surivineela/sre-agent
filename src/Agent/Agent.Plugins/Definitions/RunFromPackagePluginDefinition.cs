// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.ComponentModel;
using System.Net.Http;
using System.Threading.Tasks;
using Agent.Core.Attributes;
using Agent.Core.Clients.Storage;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Framework;
using Agent.Plugins.Implementation;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using Agent.Plugins.Models.RunFromPackage;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Definitions
{
    /// <summary>
    /// Definition for RunFromPackagePlugin to be used with dependency injection and agent tools
    /// </summary>
    [AgentToolPlugin(Category = ToolCategories.Deployment, ResourceType = ToolResourceTypes.AppService)]
    public class RunFromPackagePluginDefinition
    {
        private readonly IRunFromPackagePlugin _runFromPackagePlugin;

        /// <summary>
        /// Constructor for RunFromPackagePluginDefinition
        /// </summary>
        /// <param name="logger">Logger for the plugin</param>
        /// <param name="armHelper">ARM helper for interacting with Azure resources</param>
        /// <param name="httpClientFactory">HTTP client factory for making HTTP requests</param>
        /// <param name="authService">Authentication service for Azure resources</param>
        /// <param name="blobStorageClient">Azure Blob Storage client for package inspection</param>
        public RunFromPackagePluginDefinition(
            ILogger<RunFromPackagePlugin> logger,
            ArmHelper armHelper,
            IHttpClientFactory httpClientFactory,
            IAuthenticationService authService,
            IAzureBlobStorageClient blobStorageClient)
        {
            _runFromPackagePlugin = new RunFromPackagePlugin(logger, armHelper, httpClientFactory, authService, blobStorageClient);
        }

        #region Verification Tools

        /// <summary>
        /// Gets the current WEBSITE_RUN_FROM_PACKAGE configuration
        /// </summary>
        [Description("Retrieves the current WEBSITE_RUN_FROM_PACKAGE configuration for a Function App or Web App. " +
                    "Analyzes the current setting value, determines the mode (None, LocalPackage, ExternalUrl, Invalid), " +
                    "and provides SKU-specific validity assessment. Returns comprehensive configuration details including " +
                    "current value, expected format, and configuration validity status.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<RunFromPackageConfiguration> GetRunFromPackageConfigurationAsync(
            [Description("The full Azure resource ID of the Function App or Web App to analyze.")] string resourceId)
        {
            return await _runFromPackagePlugin.GetRunFromPackageConfigurationAsync(resourceId);
        }

        /// <summary>
        /// Verifies if the WEBSITE_RUN_FROM_PACKAGE configuration is valid
        /// </summary>
        [Description("Performs comprehensive verification of WEBSITE_RUN_FROM_PACKAGE configuration validity. " +
                    "Checks setting existence, value format, SKU compatibility, and package accessibility. " +
                    "Returns detailed verification results with status (Valid/Invalid/Missing/Suboptimal), " +
                    "current and recommended values, identified issues, and actionable recommendations.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<RunFromPackageVerificationResult> VerifyRunFromPackageConfigurationAsync(
            [Description("The full Azure resource ID of the Function App or Web App to verify.")] string resourceId)
        {
            return await _runFromPackagePlugin.VerifyRunFromPackageConfigurationAsync(resourceId);
        }

        /// <summary>
        /// Validates that the package URL is accessible
        /// </summary>
        [Description("Validates package URL accessibility without exposing sensitive information. " +
                    "Tests URL reachability, authentication requirements, and file format validation. " +
                    "Supports both Azure Blob Storage URLs and public URLs. Returns accessibility status, " +
                    "response codes, error details (sanitized), and authentication requirements.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<PackageAccessibilityResult> ValidatePackageAccessibilityAsync(
            [Description("The full Azure resource ID of the Function App or Web App.")] string resourceId,
            [Description("Optional package URL to validate. If not provided, the URL from WEBSITE_RUN_FROM_PACKAGE app setting will be used.")] string packageUrl = "")
        {
            return await _runFromPackagePlugin.ValidatePackageAccessibilityAsync(resourceId, packageUrl);
        }

        /// <summary>
        /// Inspects the package structure without exposing the contents
        /// </summary>
        [Description("Inspects package structure and validates format without exposing sensitive content. " +
                    "Checks for required files (like host.json), validates folder structure, and provides " +
                    "package statistics (size, file count). Returns structure validation results and " +
                    "recommendations without revealing package contents or sensitive information.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<PackageStructureReport> InspectPackageStructureAsync(
            [Description("The full Azure resource ID of the Function App or Web App.")] string resourceId,
            [Description("Optional package URL to inspect. If not provided, the URL from WEBSITE_RUN_FROM_PACKAGE app setting will be used.")] string packageUrl = "")
        {
            return await _runFromPackagePlugin.InspectPackageStructureAsync(resourceId, packageUrl);
        }

        #endregion

        #region Diagnostic Tools

        /// <summary>
        /// Performs a comprehensive diagnosis of the WEBSITE_RUN_FROM_PACKAGE configuration
        /// </summary>
        [Description("Performs comprehensive diagnosis of WEBSITE_RUN_FROM_PACKAGE configuration and related issues. " +
                    "Analyzes SKU compatibility, configuration validity, package accessibility, storage health, " +
                    "network connectivity, and permissions. Returns detailed diagnostic report with categorized " +
                    "issues, severity levels, overall health status, and prioritized recommendations.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<RunFromPackageDiagnosticReport> DiagnoseRunFromPackageIssuesAsync(
            [Description("The full Azure resource ID of the Function App or Web App to diagnose.")] string resourceId)
        {
            return await _runFromPackagePlugin.DiagnoseRunFromPackageIssuesAsync(resourceId);
        }

        /// <summary>
        /// Gets metadata about the package without retrieving its contents
        /// </summary>
        [Description("Retrieves package metadata without exposing sensitive content or downloading the package. " +
                    "Returns package information including last modified date, size, content type, ETag, " +
                    "and storage account name (sanitized). Provides essential package information while " +
                    "maintaining security and avoiding content exposure.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<PackageMetadata> GetPackageMetadataAsync(
            [Description("The full Azure resource ID of the Function App or Web App.")] string resourceId,
            [Description("Optional package URL to get metadata for. If not provided, the URL from WEBSITE_RUN_FROM_PACKAGE app setting will be used.")] string packageUrl = "")
        {
            return await _runFromPackagePlugin.GetPackageMetadataAsync(resourceId, packageUrl);
        }

        #endregion

        #region Repair Tools

        /// <summary>
        /// Repairs the WEBSITE_RUN_FROM_PACKAGE configuration
        /// </summary>
        [Description("Repairs WEBSITE_RUN_FROM_PACKAGE configuration issues with user consent. " +
                    "Supports multiple repair actions: SetToOne (local package mode), UpdateUrl (new URL), " +
                    "RemoveSetting (disable feature). Validates changes before applying and provides " +
                    "detailed results including previous and new values (sanitized for security).")]
        [AgentTool(ToolMode.Auto)]
        [RequiresApproval]
        [WriteAction]
        public async Task<RunFromPackageRepairResult> RepairRunFromPackageConfigurationAsync(
            [Description("The full Azure resource ID of the Function App or Web App to repair.")] string resourceId,
            [Description("The type of repair action to perform: SetToOne, UpdateUrl, or RemoveSetting.")] RunFromPackageRepairAction repairAction,
            [Description("Optional new value for the setting when using UpdateUrl action.")] string newValue = "")
        {
            return await _runFromPackagePlugin.RepairRunFromPackageConfigurationAsync(resourceId, repairAction, newValue);
        }

        /// <summary>
        /// Generates a temporary SAS URL for accessing a package
        /// </summary>
        [Description("Generates a secure, temporary SAS URL for package access without exposing storage keys. " +
                    "Creates time-limited access tokens for Azure Blob Storage packages. Returns ready-to-use " +
                    "SAS URL with specified expiry time while maintaining security by not exposing underlying " +
                    "storage account credentials or keys.")]
        [AgentTool(ToolMode.Auto)]
        [RequiresApproval]
        public async Task<SasUrlGenerationResult> GeneratePackageSasUrlAsync(
            [Description("The full Azure resource ID of the Function App or Web App.")] string resourceId,
            [Description("The name of the storage account containing the package.")] string storageAccountName,
            [Description("The name of the container containing the package.")] string containerName,
            [Description("The name of the blob (package file).")] string blobName,
            [Description("Number of hours until the SAS URL expires (default: 24 hours).")] int expiryHours = 24)
        {
            return await _runFromPackagePlugin.GeneratePackageSasUrlAsync(resourceId, storageAccountName, containerName, blobName, expiryHours);
        }

        /// <summary>
        /// Migrates from an external URL to local package mode
        /// </summary>
        [Description("Migrates WEBSITE_RUN_FROM_PACKAGE configuration from external URL mode to local package mode. " +
                    "Analyzes current configuration, validates migration feasibility, and provides step-by-step " +
                    "migration guidance. Returns migration plan with verification status and recommended actions " +
                    "for converting to local package deployment mode.")]
        [AgentTool(ToolMode.Auto)]
        [RequiresApproval]
        [WriteAction]
        public async Task<MigrationResult> MigrateToLocalPackageAsync(
            [Description("The full Azure resource ID of the Function App or Web App to migrate.")] string resourceId)
        {
            return await _runFromPackagePlugin.MigrateToLocalPackageAsync(resourceId);
        }

        #endregion

        #region Utility Tools

        /// <summary>
        /// Checks if the resource has issues with WEBSITE_RUN_FROM_PACKAGE configuration
        /// </summary>
        [Description("Quick check to determine if a Function App or Web App has WEBSITE_RUN_FROM_PACKAGE configuration issues. " +
                    "Performs rapid assessment to identify if specialized WEBSITE_RUN_FROM_PACKAGE diagnosis is needed. " +
                    "Returns boolean result indicating whether issues are detected, enabling efficient triage and handoff decisions.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<bool> HasRunFromPackageIssuesAsync(
            [Description("The full Azure resource ID of the Function App or Web App to check.")] string resourceId)
        {
            return await _runFromPackagePlugin.HasRunFromPackageIssuesAsync(resourceId);
        }

        /// <summary>
        /// Gets the SKU and capabilities related to WEBSITE_RUN_FROM_PACKAGE for a resource
        /// </summary>
        [Description("Retrieves SKU-specific capabilities and recommendations for WEBSITE_RUN_FROM_PACKAGE configuration. " +
                    "Analyzes hosting plan tier, operating system, and provides detailed information about supported " +
                    "modes (LocalPackage, ExternalUrl), recommended configurations, and SKU-specific limitations.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<SkuCapabilities> GetSkuCapabilitiesAsync(
            [Description("The full Azure resource ID of the Function App or Web App to analyze.")] string resourceId)
        {
            return await _runFromPackagePlugin.GetSkuCapabilitiesAsync(resourceId);
        }

        /// <summary>
        /// Retrieves a summary of recommended WEBSITE_RUN_FROM_PACKAGE configuration
        /// </summary>
        [Description("Provides comprehensive summary of recommended WEBSITE_RUN_FROM_PACKAGE configuration based on SKU analysis. " +
                    "Returns detailed recommendations including current configuration assessment, supported modes, " +
                    "optimal settings for the specific SKU and OS, limitations, and best practices guidance.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> GetRunFromPackageRecommendationsAsync(
            [Description("The full Azure resource ID of the Function App or Web App to get recommendations for.")] string resourceId)
        {
            return await _runFromPackagePlugin.GetRunFromPackageRecommendationsAsync(resourceId);
        }

        /// <summary>
        /// Verifies files in a blob container
        /// </summary>
        [Description("Verifies files in an Azure Storage blob container or confirms local package mode configuration. " +
                    "When WEBSITE_RUN_FROM_PACKAGE is set to '1' (local package mode), confirms this valid configuration. " +
                    "When set to a URL, lists all files in the specified container and validates target file existence. " +
                    "Provides detailed file information including names, sizes, content types, and modification dates.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<BlobContainerVerificationResult> VerifyFilesInBlobContainerAsync(
            [Description("The full Azure resource ID of the Function App or Web App to verify.")] string resourceId,
            [Description("Optional path to the blob container. If not provided, container information will be extracted from WEBSITE_RUN_FROM_PACKAGE app setting.")] string containerPath = "")
        {
            return await _runFromPackagePlugin.VerifyFilesInBlobContainerAsync(resourceId, containerPath);
        }

        #endregion
    }
}