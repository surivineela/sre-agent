// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Agent.Plugins.Models.RunFromPackage
{
    /// <summary>
    /// Comprehensive diagnostic report for WEBSITE_RUN_FROM_PACKAGE configuration
    /// </summary>
    public class RunFromPackageDiagnosticReport
    {
        /// <summary>
        /// Constructor for RunFromPackageDiagnosticReport
        /// </summary>
        public RunFromPackageDiagnosticReport()
        {
            ResourceId = string.Empty;
            Sku = string.Empty;
            ErrorMessage = string.Empty;
            Summary = string.Empty;
            OperatingSystem = string.Empty;
            Configuration = new RunFromPackageConfiguration();
        }

        /// <summary>
        /// The Azure resource ID of the Function App or Web App
        /// </summary>
        public string ResourceId { get; set; }

        /// <summary>
        /// SKU of the Function App or Web App
        /// </summary>
        public string Sku { get; set; }

        /// <summary>
        /// Whether the SKU supports WEBSITE_RUN_FROM_PACKAGE
        /// </summary>
        public bool SkuSupportsFeature { get; set; }

        /// <summary>
        /// Issues with the configuration
        /// </summary>
        public List<DiagnosticIssue> ConfigurationIssues { get; set; } = new List<DiagnosticIssue>();

        /// <summary>
        /// Issues with storage access
        /// </summary>
        public List<DiagnosticIssue> StorageIssues { get; set; } = new List<DiagnosticIssue>();

        /// <summary>
        /// Network-related issues
        /// </summary>
        public List<DiagnosticIssue> NetworkIssues { get; set; } = new List<DiagnosticIssue>();

        /// <summary>
        /// Permission-related issues
        /// </summary>
        public List<DiagnosticIssue> PermissionIssues { get; set; } = new List<DiagnosticIssue>();

        /// <summary>
        /// Overall health status
        /// </summary>
        public HealthStatus OverallStatus { get; set; }

        /// <summary>
        /// Current configuration
        /// </summary>
        public RunFromPackageConfiguration Configuration { get; set; }

        /// <summary>
        /// Whether the diagnostic operation was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Error message if diagnosis failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Summary of the diagnostic findings
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        /// Recommended actions
        /// </summary>
        public List<string> Recommendations { get; set; } = new List<string>();

        /// <summary>
        /// Operating system of the app (Windows/Linux)
        /// </summary>
        public string OperatingSystem { get; set; }
    }

    /// <summary>
    /// Represents a diagnostic issue
    /// </summary>
    public class DiagnosticIssue
    {
        /// <summary>
        /// Constructor for DiagnosticIssue
        /// </summary>
        public DiagnosticIssue()
        {
            Description = string.Empty;
        }

        /// <summary>
        /// Type of issue
        /// </summary>
        public IssueType Type { get; set; }

        /// <summary>
        /// Severity of the issue
        /// </summary>
        public IssueSeverity Severity { get; set; }

        /// <summary>
        /// Description of the issue
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Recommended actions to fix the issue
        /// </summary>
        public List<string> RecommendedActions { get; set; } = new List<string>();

        /// <summary>
        /// Whether the issue is fixable
        /// </summary>
        public bool IsFixable { get; set; }

        /// <summary>
        /// Fix action that can be performed
        /// </summary>
        public RunFromPackageRepairAction? FixAction { get; set; }
    }

    /// <summary>
    /// Type of diagnostic issue
    /// </summary>
    public enum IssueType
    {
        /// <summary>
        /// Issue with configuration
        /// </summary>
        Configuration,

        /// <summary>
        /// Issue with storage access
        /// </summary>
        Storage,

        /// <summary>
        /// Issue with network
        /// </summary>
        Network,

        /// <summary>
        /// Issue with permissions
        /// </summary>
        Permission,

        /// <summary>
        /// Issue with SKU compatibility
        /// </summary>
        SkuCompatibility,

        /// <summary>
        /// Issue with package structure
        /// </summary>
        PackageStructure
    }

    /// <summary>
    /// Severity of a diagnostic issue
    /// </summary>
    public enum IssueSeverity
    {
        /// <summary>
        /// Critical issue that prevents functionality
        /// </summary>
        Critical,

        /// <summary>
        /// High severity issue
        /// </summary>
        High,

        /// <summary>
        /// Medium severity issue
        /// </summary>
        Medium,

        /// <summary>
        /// Low severity issue
        /// </summary>
        Low,

        /// <summary>
        /// Informational note
        /// </summary>
        Info
    }

    /// <summary>
    /// Overall health status
    /// </summary>
    public enum HealthStatus
    {
        /// <summary>
        /// Healthy configuration
        /// </summary>
        Healthy,

        /// <summary>
        /// Degraded but functional
        /// </summary>
        Degraded,

        /// <summary>
        /// Unhealthy configuration
        /// </summary>
        Unhealthy,

        /// <summary>
        /// Critical issues detected
        /// </summary>
        Critical,

        /// <summary>
        /// Status cannot be determined
        /// </summary>
        Unknown
    }
}
