// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Agent.Plugins.Models.RunFromPackage
{
    /// <summary>
    /// Result of a repair operation for WEBSITE_RUN_FROM_PACKAGE
    /// </summary>
    public class RunFromPackageRepairResult
    {
        /// <summary>
        /// Constructor for RunFromPackageRepairResult
        /// </summary>
        public RunFromPackageRepairResult()
        {
            ResourceId = string.Empty;
            ActionTaken = string.Empty;
            PreviousValue = string.Empty;
            NewValue = string.Empty;
            ErrorMessage = string.Empty;
            Details = string.Empty;
        }

        /// <summary>
        /// The Azure resource ID of the Function App or Web App
        /// </summary>
        public string ResourceId { get; set; }

        /// <summary>
        /// Whether the repair was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Action that was taken
        /// </summary>
        public string ActionTaken { get; set; }

        /// <summary>
        /// Previous value of the setting (sanitized)
        /// </summary>
        public string PreviousValue { get; set; }

        /// <summary>
        /// New value of the setting (sanitized)
        /// </summary>
        public string NewValue { get; set; }

        /// <summary>
        /// Type of repair action performed
        /// </summary>
        public RunFromPackageRepairAction RepairAction { get; set; }

        /// <summary>
        /// Timestamp of the repair
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Error message if repair failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Detailed message about the repair
        /// </summary>
        public string Details { get; set; }
    }

    /// <summary>
    /// Type of repair action for WEBSITE_RUN_FROM_PACKAGE
    /// </summary>
    public enum RunFromPackageRepairAction
    {
        /// <summary>
        /// Set WEBSITE_RUN_FROM_PACKAGE to "1" for local package
        /// </summary>
        SetToOne,

        /// <summary>
        /// Update the URL in WEBSITE_RUN_FROM_PACKAGE
        /// </summary>
        UpdateUrl,

        /// <summary>
        /// Remove the WEBSITE_RUN_FROM_PACKAGE setting
        /// </summary>
        RemoveSetting,

        /// <summary>
        /// Generate a new SAS URL
        /// </summary>
        GenerateSasUrl,

        /// <summary>
        /// Fix storage permissions
        /// </summary>
        FixStoragePermissions,

        /// <summary>
        /// Migrate to local package mode
        /// </summary>
        MigrateToLocalPackage,

        /// <summary>
        /// Set value for this SKU and OS
        /// </summary>
        SetRecommendedValue
    }

    /// <summary>
    /// Result of generating a SAS URL
    /// </summary>
    public class SasUrlGenerationResult
    {
        /// <summary>
        /// Constructor for SasUrlGenerationResult
        /// </summary>
        public SasUrlGenerationResult()
        {
            SasUrl = string.Empty;
            ErrorMessage = string.Empty;
            StorageAccountName = string.Empty;
            ContainerName = string.Empty;
            BlobName = string.Empty;
        }

        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// SAS URL (sanitized, no exposed keys)
        /// </summary>
        public string SasUrl { get; set; }

        /// <summary>
        /// When the SAS URL expires
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Error message if generation failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Storage account name
        /// </summary>
        public string StorageAccountName { get; set; }

        /// <summary>
        /// Container name
        /// </summary>
        public string ContainerName { get; set; }

        /// <summary>
        /// Blob name
        /// </summary>
        public string BlobName { get; set; }
    }

    /// <summary>
    /// Result of migrating to local package mode
    /// </summary>
    public class MigrationResult
    {
        /// <summary>
        /// Constructor for MigrationResult
        /// </summary>
        public MigrationResult()
        {
            VerificationStatus = string.Empty;
            ErrorMessage = string.Empty;
            PreviousConfiguration = new RunFromPackageConfiguration();
            NewConfiguration = new RunFromPackageConfiguration();
            MigrationSteps = new List<string>();
        }

        /// <summary>
        /// Whether the migration was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Steps taken during migration
        /// </summary>
        public List<string> MigrationSteps { get; set; } = new List<string>();

        /// <summary>
        /// Verification status after migration
        /// </summary>
        public string VerificationStatus { get; set; }

        /// <summary>
        /// Error message if migration failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Previous configuration
        /// </summary>
        public RunFromPackageConfiguration PreviousConfiguration { get; set; }

        /// <summary>
        /// New configuration
        /// </summary>
        public RunFromPackageConfiguration NewConfiguration { get; set; }
    }
}