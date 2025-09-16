// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Generic;

namespace Agent.Plugins.Models.RunFromPackage
{
    /// <summary>
    /// Represents capabilities of different SKUs for WEBSITE_RUN_FROM_PACKAGE
    /// </summary>
    public class SkuCapabilities
    {
        /// <summary>
        /// Constructor with default values
        /// </summary>
        public SkuCapabilities()
        {
            SkuName = string.Empty;
            OperatingSystem = string.Empty;
            RecommendedValue = string.Empty;
            Details = string.Empty;
            Limitations = new List<string>();
        }

        /// <summary>
        /// The SKU name
        /// </summary>
        public string SkuName { get; set; }

        /// <summary>
        /// Operating system (Windows/Linux)
        /// </summary>
        public string OperatingSystem { get; set; }

        /// <summary>
        /// Whether local package mode is supported
        /// </summary>
        public bool SupportsLocalPackage { get; set; }

        /// <summary>
        /// Whether external URL is supported
        /// </summary>
        public bool SupportsExternalUrl { get; set; }

        /// <summary>
        /// The recommended mode for this SKU
        /// </summary>
        public RunFromPackageMode RecommendedMode { get; set; }

        /// <summary>
        /// Recommended value for WEBSITE_RUN_FROM_PACKAGE
        /// </summary>
        public string RecommendedValue { get; set; }

        /// <summary>
        /// Limitations for this SKU
        /// </summary>
        public List<string> Limitations { get; set; } = new List<string>();

        /// <summary>
        /// Additional details about this SKU's capabilities
        /// </summary>
        public string Details { get; set; }

        /// <summary>
        /// Checks if a specific mode is supported
        /// </summary>
        /// <param name="mode">The mode to check</param>
        /// <returns>True if the mode is supported, false otherwise</returns>
        public bool SupportsMode(RunFromPackageMode mode)
        {
            switch (mode)
            {
                case RunFromPackageMode.LocalPackage:
                    return SupportsLocalPackage;
                case RunFromPackageMode.ExternalUrl:
                    return SupportsExternalUrl;
                case RunFromPackageMode.None:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Get SKU capabilities for a specific SKU and OS
        /// </summary>
        /// <param name="skuName">The SKU name</param>
        /// <param name="operatingSystem">The operating system (Windows/Linux)</param>
        /// <returns>The capabilities for the SKU</returns>
        public static SkuCapabilities GetForSku(string skuName, string operatingSystem)
        {
            // Normalize SKU name for comparison
            string normalizedSku = (skuName ?? string.Empty).ToLowerInvariant();
            string normalizedOs = (operatingSystem ?? "Windows").ToLowerInvariant();
            bool isLinux = normalizedOs.Contains("linux");

            // Define common SKU groups
            bool isConsumption = normalizedSku.Contains("consumption") || normalizedSku.Contains("dynamic");
            bool isPremium = normalizedSku.Contains("premium") || normalizedSku.Contains("elastic");
            bool isDedicated = normalizedSku.Contains("standard") || normalizedSku.Contains("basic") || normalizedSku.Contains("isolated");
            bool isFlexConsumption = normalizedSku.Contains("flex");
            bool isFree = normalizedSku.Contains("free") || normalizedSku.Contains("shared");

            // Create capabilities based on SKU and OS
            var capabilities = new SkuCapabilities
            {
                SkuName = skuName ?? string.Empty,
                OperatingSystem = operatingSystem ?? string.Empty
            };

            if (isFlexConsumption)
            {
                capabilities.SupportsExternalUrl = true;
                capabilities.SupportsLocalPackage = !isLinux; // Linux FlexConsumption doesn't support local package
                capabilities.RecommendedMode = RunFromPackageMode.None; // Flex Consumption runs from package by default
                capabilities.RecommendedValue = ""; // Blank or not set
                
                if (isLinux)
                {
                    capabilities.Details = "Flex Consumption plan runs from a package by default, no WEBSITE_RUN_FROM_PACKAGE setting is required. Linux only supports external URL mode.";
                }
                else
                {
                    capabilities.Details = "Flex Consumption plan runs from a package by default, no WEBSITE_RUN_FROM_PACKAGE setting is required.";
                }
            }
            else if (isConsumption)
            {
                capabilities.SupportsExternalUrl = true;
                capabilities.SupportsLocalPackage = !isLinux; // Linux Consumption doesn't support local package
                capabilities.RecommendedMode = isLinux ? RunFromPackageMode.ExternalUrl : RunFromPackageMode.LocalPackage;
                capabilities.RecommendedValue = isLinux ? "<URL>" : "1";
                
                if (isLinux)
                {
                    capabilities.Limitations.Add("Linux Consumption requires WEBSITE_RUN_FROM_PACKAGE to be set to a URL");
                    capabilities.Details = "Linux Consumption only supports external URL mode, local package mode is not supported.";
                }
                else
                {
                    capabilities.Details = "Windows Consumption supports both local package and external URL modes, with local package ('1') being recommended.";
                }
            }
            else if (isPremium || isDedicated)
            {
                capabilities.SupportsExternalUrl = true;
                capabilities.SupportsLocalPackage = true;
                capabilities.RecommendedMode = RunFromPackageMode.LocalPackage;
                capabilities.RecommendedValue = "1";
                capabilities.Details = $"{skuName} supports both local package and external URL modes, with local package ('1') being recommended.";
            }
            else if (isFree)
            {
                capabilities.SupportsExternalUrl = true;
                capabilities.SupportsLocalPackage = !isLinux;
                capabilities.RecommendedMode = isLinux ? RunFromPackageMode.ExternalUrl : RunFromPackageMode.LocalPackage;
                capabilities.RecommendedValue = isLinux ? "<URL>" : "1";
                capabilities.Limitations.Add("Free/Shared SKU may have limited performance and storage capabilities.");
                capabilities.Details = "Free/Shared SKU has limitations on performance and storage, but WEBSITE_RUN_FROM_PACKAGE can still be used.";
            }
            else
            {
                // Default/unknown SKU
                capabilities.SupportsExternalUrl = true;
                capabilities.SupportsLocalPackage = !isLinux;
                capabilities.RecommendedMode = isLinux ? RunFromPackageMode.ExternalUrl : RunFromPackageMode.LocalPackage;
                capabilities.RecommendedValue = isLinux ? "<URL>" : "1";
                capabilities.Details = "Unknown or custom SKU. Assuming standard capabilities.";
            }

            return capabilities;
        }
    }
}