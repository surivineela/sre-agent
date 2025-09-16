// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Agent.Plugins.Models.RunFromPackage
{
    /// <summary>
    /// Represents the current WEBSITE_RUN_FROM_PACKAGE configuration
    /// </summary>
    public class RunFromPackageConfiguration
    {
        /// <summary>
        /// Constructor for RunFromPackageConfiguration
        /// </summary>
        public RunFromPackageConfiguration()
        {
            ResourceId = string.Empty;
            ResourceType = string.Empty;
            CurrentValue = string.Empty;
            ExpectedFormat = string.Empty;
            Sku = string.Empty;
            OperatingSystem = string.Empty;
            Details = string.Empty;
        }

        /// <summary>
        /// The Azure resource ID of the Function App or Web App
        /// </summary>
        public string ResourceId { get; set; }

        /// <summary>
        /// The type of resource (FunctionApp or WebApp)
        /// </summary>
        public string ResourceType { get; set; }

        /// <summary>
        /// The current value of the WEBSITE_RUN_FROM_PACKAGE setting
        /// </summary>
        public string CurrentValue { get; set; }

        /// <summary>
        /// Whether the WEBSITE_RUN_FROM_PACKAGE setting exists
        /// </summary>
        public bool SettingExists { get; set; }

        /// <summary>
        /// The expected format based on the app's SKU and configuration
        /// </summary>
        public string ExpectedFormat { get; set; }

        /// <summary>
        /// True if the setting value is valid for the current SKU
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// The SKU of the Function App or Web App
        /// </summary>
        public string Sku { get; set; }

        /// <summary>
        /// The OS of the Function App or Web App (Windows/Linux)
        /// </summary>
        public string OperatingSystem { get; set; }

        /// <summary>
        /// Mode of operation (LocalPackage, ExternalUrl, None)
        /// </summary>
        public RunFromPackageMode Mode { get; set; }

        /// <summary>
        /// Timestamp of when the configuration was last modified
        /// </summary>
        public DateTime? LastModified { get; set; }

        /// <summary>
        /// Additional details about the configuration
        /// </summary>
        public string Details { get; set; }
    }

    /// <summary>
    /// Mode of WEBSITE_RUN_FROM_PACKAGE operation
    /// </summary>
    public enum RunFromPackageMode
    {
        /// <summary>
        /// WEBSITE_RUN_FROM_PACKAGE is not set
        /// </summary>
        None,

        /// <summary>
        /// WEBSITE_RUN_FROM_PACKAGE is set to "1" for local package
        /// </summary>
        LocalPackage,

        /// <summary>
        /// WEBSITE_RUN_FROM_PACKAGE is set to a URL
        /// </summary>
        ExternalUrl,

        /// <summary>
        /// WEBSITE_RUN_FROM_PACKAGE is set to an invalid value
        /// </summary>
        Invalid
    }
}