// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Generic;

namespace Agent.Plugins.Models.RunFromPackage
{
    /// <summary>
    /// Result of verifying the WEBSITE_RUN_FROM_PACKAGE configuration
    /// </summary>
    public class RunFromPackageVerificationResult
    {
        /// <summary>
        /// Constructor for RunFromPackageVerificationResult
        /// </summary>
        public RunFromPackageVerificationResult()
        {
            ResourceId = string.Empty;
            CurrentValue = string.Empty;
            RecommendedValue = string.Empty;
            Sku = string.Empty;
            OperatingSystem = string.Empty;
            Details = string.Empty;
            ErrorMessage = string.Empty;
            Issues = new List<string>();
            Recommendations = new List<string>();
        }

        /// <summary>
        /// The Azure resource ID of the Function App or Web App
        /// </summary>
        public string ResourceId { get; set; }

        /// <summary>
        /// Overall status of the verification
        /// </summary>
        public ConfigurationStatus Status { get; set; }

        /// <summary>
        /// Current value of WEBSITE_RUN_FROM_PACKAGE (sanitized)
        /// </summary>
        public string CurrentValue { get; set; }

        /// <summary>
        /// The recommended value based on SKU
        /// </summary>
        public string RecommendedValue { get; set; }

        /// <summary>
        /// Issues found during verification
        /// </summary>
        public List<string> Issues { get; set; } = new List<string>();

        /// <summary>
        /// Whether the resource supports WEBSITE_RUN_FROM_PACKAGE
        /// </summary>
        public bool IsSupported { get; set; }

        /// <summary>
        /// The SKU of the Function App or Web App
        /// </summary>
        public string Sku { get; set; }

        /// <summary>
        /// The operating system of the app (Windows/Linux)
        /// </summary>
        public string OperatingSystem { get; set; }

        /// <summary>
        /// Detailed description of the verification result
        /// </summary>
        public string Details { get; set; }

        /// <summary>
        /// Recommended actions to fix issues
        /// </summary>
        public List<string> Recommendations { get; set; } = new List<string>();

        /// <summary>
        /// Whether verification was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Error message if verification failed
        /// </summary>
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Status of the WEBSITE_RUN_FROM_PACKAGE configuration
    /// </summary>
    public enum ConfigurationStatus
    {
        /// <summary>
        /// Configuration is valid and appropriate for the SKU
        /// </summary>
        Valid,

        /// <summary>
        /// Configuration is missing
        /// </summary>
        Missing,

        /// <summary>
        /// Configuration is present but invalid
        /// </summary>
        Invalid,

        /// <summary>
        /// Configuration is present but not optimal for the SKU
        /// </summary>
        Suboptimal,

        /// <summary>
        /// Configuration is not supported for this SKU
        /// </summary>
        Unsupported,

        /// <summary>
        /// Configuration cannot be verified
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Result of validating package accessibility
    /// </summary>
    public class PackageAccessibilityResult
    {
        /// <summary>
        /// Constructor for PackageAccessibilityResult
        /// </summary>
        public PackageAccessibilityResult()
        {
            ResourceId = string.Empty;
            PackageUrl = string.Empty;
            ErrorDetails = string.Empty;
            StorageType = string.Empty;
            Recommendations = new List<string>();
        }

        /// <summary>
        /// The Azure resource ID of the Function App or Web App
        /// </summary>
        public string ResourceId { get; set; }

        /// <summary>
        /// Package URL being validated (sanitized)
        /// </summary>
        public string PackageUrl { get; set; }

        /// <summary>
        /// Whether the package is accessible
        /// </summary>
        public bool IsAccessible { get; set; }

        /// <summary>
        /// HTTP response code (if applicable)
        /// </summary>
        public int? ResponseCode { get; set; }

        /// <summary>
        /// Error details (sanitized)
        /// </summary>
        public string ErrorDetails { get; set; }

        /// <summary>
        /// Whether authentication is required
        /// </summary>
        public bool RequiresAuthentication { get; set; }

        /// <summary>
        /// Type of storage (Azure Blob, public URL, etc.)
        /// </summary>
        public string StorageType { get; set; }

        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Recommendations to fix access issues
        /// </summary>
        public List<string> Recommendations { get; set; } = new List<string>();
    }
}