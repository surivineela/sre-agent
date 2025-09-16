// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Agent.Plugins.Models.RunFromPackage
{
    /// <summary>
    /// Report on the structure of a package without exposing its contents
    /// </summary>
    public class PackageStructureReport
    {
        /// <summary>
        /// Constructor for PackageStructureReport
        /// </summary>
        public PackageStructureReport()
        {
            ResourceId = string.Empty;
            PackageUrl = string.Empty;
            Details = string.Empty;
            ErrorMessage = string.Empty;
            DetectedRuntime = string.Empty;
            RootStructure = string.Empty;
        }

        /// <summary>
        /// The Azure resource ID of the Function App or Web App
        /// </summary>
        public string ResourceId { get; set; }

        /// <summary>
        /// Package URL that was inspected (sanitized)
        /// </summary>
        public string PackageUrl { get; set; }

        /// <summary>
        /// Whether the package has all required files
        /// </summary>
        public bool HasRequiredFiles { get; set; }

        /// <summary>
        /// Whether the package has a valid structure for a Function App
        /// </summary>
        public bool HasValidStructure { get; set; }

        /// <summary>
        /// Detected runtime (.NET, Node.js, Python, Java, PowerShell, etc.)
        /// </summary>
        public string DetectedRuntime { get; set; }

        /// <summary>
        /// Whether host.json is found at the root level
        /// </summary>
        public bool HasHostJson { get; set; }

        /// <summary>
        /// Number of functions detected in the package
        /// </summary>
        public int FunctionCount { get; set; }

        /// <summary>
        /// List of function names found in the package
        /// </summary>
        public List<string> Functions { get; set; } = new List<string>();

        /// <summary>
        /// Missing components in the package
        /// </summary>
        public List<string> MissingComponents { get; set; } = new List<string>();

        /// <summary>
        /// Missing required files based on detected runtime
        /// </summary>
        public List<string> MissingRequiredFiles { get; set; } = new List<string>();

        /// <summary>
        /// Issues found with the package structure
        /// </summary>
        public List<string> StructureIssues { get; set; } = new List<string>();

        /// <summary>
        /// Whether the folder structure is valid
        /// </summary>
        public bool FolderStructureValid { get; set; }

        /// <summary>
        /// Size of the package in bytes
        /// </summary>
        public long PackageSize { get; set; }

        /// <summary>
        /// Total number of files in the package
        /// </summary>
        public int FileCount { get; set; }

        /// <summary>
        /// Count of different file types in the package
        /// </summary>
        public Dictionary<string, int> FileTypeCounts { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Description of the root level structure
        /// </summary>
        public string RootStructure { get; set; }

        /// <summary>
        /// Recommendations for improving the package
        /// </summary>
        public List<string> Recommendations { get; set; } = new List<string>();

        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Error message if inspection failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Additional details about the package structure
        /// </summary>
        public string Details { get; set; }

        /// <summary>
        /// Required files found in the package
        /// </summary>
        public List<string> RequiredFilesFound { get; set; } = new List<string>();
    }

    /// <summary>
    /// Metadata about a package without its contents
    /// </summary>
    public class PackageMetadata
    {
        /// <summary>
        /// Constructor for PackageMetadata
        /// </summary>
        public PackageMetadata()
        {
            PackageUrl = string.Empty;
            ContentType = string.Empty;
            ETag = string.Empty;
            StorageAccount = string.Empty;
            ContainerName = string.Empty;
            ErrorMessage = string.Empty;
        }

        /// <summary>
        /// Package URL (sanitized)
        /// </summary>
        public string PackageUrl { get; set; }

        /// <summary>
        /// When the package was last modified
        /// </summary>
        public DateTime? LastModified { get; set; }

        /// <summary>
        /// Size of the package in bytes
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Content type of the package
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// ETag of the package
        /// </summary>
        public string ETag { get; set; }

        /// <summary>
        /// Storage account name (name only, no keys or connection strings)
        /// </summary>
        public string StorageAccount { get; set; }

        /// <summary>
        /// Container name
        /// </summary>
        public string ContainerName { get; set; }

        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Error message if retrieval failed
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}