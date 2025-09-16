// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Agent.Plugins.Models;
using Agent.Plugins.Models.RunFromPackage;

namespace Agent.Plugins.Interface
{
    /// <summary>
    /// Interface for handling WEBSITE_RUN_FROM_PACKAGE configuration
    /// for both Azure Functions and Web Apps
    /// </summary>
    public interface IRunFromPackagePlugin
    {
        /// <summary>
        /// Gets or sets the thread ID
        /// </summary>
        Guid? ThreadId { get; set; }
        /// <summary>
        /// Gets the current WEBSITE_RUN_FROM_PACKAGE configuration
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <returns>The current configuration details</returns>
        Task<RunFromPackageConfiguration> GetRunFromPackageConfigurationAsync(string resourceId);

        /// <summary>
        /// Verifies if the WEBSITE_RUN_FROM_PACKAGE configuration is valid
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <returns>Verification result with details about any issues found</returns>
        Task<RunFromPackageVerificationResult> VerifyRunFromPackageConfigurationAsync(string resourceId);

        /// <summary>
        /// Validates that the package URL is accessible
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <param name="packageUrl">Optional package URL to validate. If not provided, the URL from app settings will be used</param>
        /// <returns>Result indicating accessibility of the package</returns>
        Task<PackageAccessibilityResult> ValidatePackageAccessibilityAsync(string resourceId, string packageUrl = "");

        /// <summary>
        /// Inspects the package structure without exposing the contents
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <param name="packageUrl">Optional package URL to inspect. If not provided, the URL from app settings will be used</param>
        /// <returns>Package structure report with details but without exposing sensitive content</returns>
        Task<PackageStructureReport> InspectPackageStructureAsync(string resourceId, string packageUrl = "");

        /// <summary>
        /// Performs a comprehensive diagnosis of the WEBSITE_RUN_FROM_PACKAGE configuration
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <returns>Detailed diagnostic report</returns>
        Task<RunFromPackageDiagnosticReport> DiagnoseRunFromPackageIssuesAsync(string resourceId);

        /// <summary>
        /// Gets metadata about the package without retrieving its contents
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <param name="packageUrl">Optional package URL to get metadata for. If not provided, the URL from app settings will be used</param>
        /// <returns>Package metadata</returns>
        Task<PackageMetadata> GetPackageMetadataAsync(string resourceId, string packageUrl = "");

        /// <summary>
        /// Repairs the WEBSITE_RUN_FROM_PACKAGE configuration
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <param name="repairAction">The type of repair action to perform</param>
        /// <param name="newValue">Optional new value for the setting</param>
        /// <returns>Result of the repair operation</returns>
        Task<RunFromPackageRepairResult> RepairRunFromPackageConfigurationAsync(
            string resourceId, 
            RunFromPackageRepairAction repairAction, 
            string newValue = "");

        /// <summary>
        /// Generates a temporary SAS URL for accessing a package
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <param name="storageAccountName">The name of the storage account</param>
        /// <param name="containerName">The name of the container</param>
        /// <param name="blobName">The name of the blob</param>
        /// <param name="expiryHours">Number of hours until the SAS expires</param>
        /// <returns>Result containing the SAS URL (with no exposed secrets)</returns>
        Task<SasUrlGenerationResult> GeneratePackageSasUrlAsync(
            string resourceId,
            string storageAccountName,
            string containerName,
            string blobName,
            int expiryHours = 24);

        /// <summary>
        /// Migrates from an external URL to local package mode
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <returns>Result of the migration operation</returns>
        Task<MigrationResult> MigrateToLocalPackageAsync(string resourceId);
        
        /// <summary>
        /// Checks if the resource has issues with WEBSITE_RUN_FROM_PACKAGE configuration
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <returns>True if issues are detected, otherwise false</returns>
        Task<bool> HasRunFromPackageIssuesAsync(string resourceId);

        /// <summary>
        /// Gets the SKU and capabilities related to WEBSITE_RUN_FROM_PACKAGE for a resource
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <returns>The SKU and supported WEBSITE_RUN_FROM_PACKAGE configurations</returns>
        Task<SkuCapabilities> GetSkuCapabilitiesAsync(string resourceId);

        /// <summary>
        /// Retrieves a summary of recommended WEBSITE_RUN_FROM_PACKAGE configuration
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <returns>A summary of recommended configurations based on the SKU</returns>
        Task<string> GetRunFromPackageRecommendationsAsync(string resourceId);

        /// <summary>
        /// Verifies files in a blob container
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <param name="containerPath">Optional path to the blob container. If not provided, the WEBSITE_RUN_FROM_PACKAGE app setting will be parsed to extract container information</param>
        /// <returns>A verification result containing the list of files in the container, or confirmation of local package mode if WEBSITE_RUN_FROM_PACKAGE is set to "1"</returns>
        Task<BlobContainerVerificationResult> VerifyFilesInBlobContainerAsync(string resourceId, string containerPath = "");
    }
}