// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Agent.Core;
using Agent.Core.Clients.Storage;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using Agent.Plugins.Models.RunFromPackage;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Agent.Plugins.Implementation
{
    /// <summary>
    /// Plugin for handling WEBSITE_RUN_FROM_PACKAGE configuration for Function Apps and Web Apps
    /// </summary>
    public class RunFromPackagePlugin : IRunFromPackagePlugin
    {
        private readonly ILogger<RunFromPackagePlugin> _logger;
        private readonly ArmHelper _armHelper;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuthenticationService _authService;
        private readonly IAzureBlobStorageClient _blobStorageClient;

        /// <summary>
        /// Gets or sets the thread ID
        /// </summary>
        public Guid? ThreadId { get; set; }

        /// <summary>
        /// Constructor for RunFromPackagePlugin
        /// </summary>
        /// <param name="logger">Logger for the plugin</param>
        /// <param name="armHelper">ARM helper for interacting with Azure resources</param>
        /// <param name="httpClientFactory">HTTP client factory for making HTTP requests</param>
        /// <param name="authService">Authentication service for Azure resources</param>
        /// <param name="blobStorageClient">Azure Blob Storage client for package inspection</param>
        public RunFromPackagePlugin(
            ILogger<RunFromPackagePlugin> logger,
            ArmHelper armHelper,
            IHttpClientFactory httpClientFactory,
            IAuthenticationService authService,
            IAzureBlobStorageClient blobStorageClient)
        {
            _logger = logger;
            _armHelper = armHelper;
            _httpClientFactory = httpClientFactory;
            _authService = authService;
            _blobStorageClient = blobStorageClient;
        }

        /// <summary>
        /// Gets the current WEBSITE_RUN_FROM_PACKAGE configuration
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <returns>The current configuration details</returns>
        public async Task<RunFromPackageConfiguration> GetRunFromPackageConfigurationAsync(string resourceId)
        {
            var configuration = new RunFromPackageConfiguration
            {
                ResourceId = resourceId,
                ResourceType = resourceId.Contains("/sites/") ? (resourceId.Contains("/functions/") ? "FunctionApp" : "WebApp") : "Unknown"
            };

            try
            {
                _logger.LogInternalInformation("Getting WEBSITE_RUN_FROM_PACKAGE configuration for {ResourceId}", resourceId);

                // Get app settings
                string appSettingsJson = await _armHelper.GetAppSettings(resourceId);

                if (string.IsNullOrWhiteSpace(appSettingsJson))
                {
                    configuration.SettingExists = false;
                    configuration.IsValid = false;
                    configuration.Details = "Failed to retrieve app settings";
                    return configuration;
                }

                // Parse app settings to get WEBSITE_RUN_FROM_PACKAGE value
                var appSettings = JObject.Parse(appSettingsJson);
                var properties = appSettings["properties"] as JObject;

                if (properties == null)
                {
                    configuration.SettingExists = false;
                    configuration.IsValid = false;
                    configuration.Details = "Failed to parse app settings";
                    return configuration;
                }

                if (!properties.TryGetValue("WEBSITE_RUN_FROM_PACKAGE", StringComparison.OrdinalIgnoreCase, out var runFromPackageValue))
                {
                    configuration.SettingExists = false;
                    configuration.IsValid = false;
                    configuration.Details = "WEBSITE_RUN_FROM_PACKAGE app setting not found";
                    return configuration;
                }

                configuration.SettingExists = true;
                configuration.CurrentValue = runFromPackageValue?.ToString() ?? string.Empty;

                // Get the operating system for this resource - use HTTP approach for better testability
                configuration.OperatingSystem = await GetOperatingSystemViaHttpAsync(resourceId);

                // Get the SKU for this resource - use HTTP approach for better testability
                configuration.Sku = await GetSkuViaHttpAsync(resourceId);

                // Determine the mode based on the value
                if (string.IsNullOrWhiteSpace(configuration.CurrentValue))
                {
                    configuration.Mode = RunFromPackageMode.None;
                    configuration.IsValid = false;
                    configuration.Details = "WEBSITE_RUN_FROM_PACKAGE value is empty";
                }
                else if (configuration.CurrentValue == "1")
                {
                    configuration.Mode = RunFromPackageMode.LocalPackage;
                    
                    // Check if LocalPackage mode is valid for this SKU/OS
                    var skuCapabilities = SkuCapabilities.GetForSku(configuration.Sku, configuration.OperatingSystem);
                    configuration.IsValid = skuCapabilities.SupportsLocalPackage;
                    
                    if (configuration.IsValid)
                    {
                        configuration.Details = "WEBSITE_RUN_FROM_PACKAGE is set to '1' (local package mode). Files are stored locally in the SitePackages folder.";
                    }
                    else
                    {
                        configuration.Details = $"WEBSITE_RUN_FROM_PACKAGE is set to '1' (local package mode), but this mode is not supported for {configuration.OperatingSystem} {configuration.Sku}.";
                    }
                }
                else if (Uri.TryCreate(configuration.CurrentValue, UriKind.Absolute, out Uri? uri) &&
                         (uri.Scheme == "http" || uri.Scheme == "https"))
                {
                    configuration.Mode = RunFromPackageMode.ExternalUrl;
                    
                    // Check if ExternalUrl mode is valid for this SKU/OS
                    var skuCapabilities = SkuCapabilities.GetForSku(configuration.Sku, configuration.OperatingSystem);
                    configuration.IsValid = skuCapabilities.SupportsExternalUrl;
                    
                    if (configuration.IsValid)
                    {
                        configuration.Details = "WEBSITE_RUN_FROM_PACKAGE is set to a URL for external package mode.";
                    }
                    else
                    {
                        configuration.Details = $"WEBSITE_RUN_FROM_PACKAGE is set to a URL, but external URL mode is not supported for {configuration.OperatingSystem} {configuration.Sku}.";
                    }
                }
                else
                {
                    configuration.Mode = RunFromPackageMode.Invalid;
                    configuration.IsValid = false;
                    var sanitizedValue = SanitizeUrl(configuration.CurrentValue);
                    configuration.Details = $"WEBSITE_RUN_FROM_PACKAGE has an invalid value: {sanitizedValue}. Expected either '1' for local package mode or a URL to a zip file.";
                }

                // Set the expected format based on the SKU and OS
                var recommendedCapabilities = SkuCapabilities.GetForSku(configuration.Sku, configuration.OperatingSystem);
                configuration.ExpectedFormat = recommendedCapabilities.RecommendedValue;

                return configuration;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error getting WEBSITE_RUN_FROM_PACKAGE configuration for {ResourceId}", resourceId);
                configuration.IsValid = false;
                configuration.Details = $"An error occurred while getting WEBSITE_RUN_FROM_PACKAGE configuration: {ex.Message}";
                return configuration;
            }
        }

        /// <summary>
        /// Verifies if the WEBSITE_RUN_FROM_PACKAGE configuration is valid
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <returns>Verification result with details about any issues found</returns>
        public async Task<RunFromPackageVerificationResult> VerifyRunFromPackageConfigurationAsync(string resourceId)
        {
            var result = new RunFromPackageVerificationResult
            {
                ResourceId = resourceId,
                IsSuccessful = true
            };

            try
            {
                _logger.LogInternalInformation("Verifying WEBSITE_RUN_FROM_PACKAGE configuration for {ResourceId}", resourceId);

                // Get the current configuration
                var configuration = await GetRunFromPackageConfigurationAsync(resourceId);
                
                // Store the configuration in the result to avoid duplicate calls
                result.Configuration = configuration;

                // Check if there was an error getting the configuration (not just an invalid setting)
                if (!configuration.IsValid && configuration.Details.Contains("An error occurred while getting WEBSITE_RUN_FROM_PACKAGE configuration"))
                {
                    // This indicates a system error (HTTP failure, etc.), not just missing/invalid settings
                    throw new InvalidOperationException(configuration.Details);
                }

                // Set basic properties from configuration (sanitize sensitive values)
                result.CurrentValue = configuration.Mode == RunFromPackageMode.ExternalUrl 
                    ? SanitizeUrl(configuration.CurrentValue) 
                    : configuration.CurrentValue;
                result.Sku = configuration.Sku;
                result.OperatingSystem = configuration.OperatingSystem;

                // Get the recommended value for this SKU/OS
                var skuCapabilities = SkuCapabilities.GetForSku(configuration.Sku, configuration.OperatingSystem);
                
                // Store the SKU capabilities in the result to avoid duplicate calls
                result.SkuCapabilities = skuCapabilities;
                
                result.RecommendedValue = skuCapabilities.RecommendedValue;
                result.IsSupported = skuCapabilities.SupportsMode(configuration.Mode);

                // Determine status based on configuration
                if (!configuration.SettingExists)
                {
                    result.Status = ConfigurationStatus.Missing;
                    result.Issues.Add("WEBSITE_RUN_FROM_PACKAGE setting is missing");
                    result.Recommendations.Add($"Add WEBSITE_RUN_FROM_PACKAGE app setting with value '{skuCapabilities.RecommendedValue}'.");
                    result.IsSuccessful = false;
                }
                else if (!configuration.IsValid)
                {
                    result.Status = ConfigurationStatus.Invalid;
                    var sanitizedValue = configuration.Mode == RunFromPackageMode.ExternalUrl 
                        ? SanitizeUrl(configuration.CurrentValue)
                        : configuration.CurrentValue;
                    result.Issues.Add($"WEBSITE_RUN_FROM_PACKAGE has an invalid value: {sanitizedValue}.");
                    result.Recommendations.Add($"Change WEBSITE_RUN_FROM_PACKAGE to '{skuCapabilities.RecommendedValue}'.");
                    result.IsSuccessful = false;
                }
                else if (!result.IsSupported)
                {
                    result.Status = ConfigurationStatus.Unsupported;
                    var sanitizedValue = configuration.Mode == RunFromPackageMode.ExternalUrl 
                        ? SanitizeUrl(configuration.CurrentValue)
                        : configuration.CurrentValue;
                    result.Issues.Add($"The current value '{sanitizedValue}' is not supported for {configuration.OperatingSystem} {configuration.Sku}.");
                    result.Recommendations.Add($"Change WEBSITE_RUN_FROM_PACKAGE to '{skuCapabilities.RecommendedValue}'.");
                    result.IsSuccessful = false;
                }
                else if (configuration.Mode == RunFromPackageMode.ExternalUrl)
                {
                    // For external URL mode, verify the URL is accessible using secure validation
                    var accessibilityResult = await ValidateWithoutExposing(resourceId, configuration.CurrentValue);
                    
                    if (!accessibilityResult.IsAccessible)
                    {
                        result.Status = ConfigurationStatus.Invalid;
                        result.Issues.Add($"The package URL is not accessible: {accessibilityResult.ErrorDetails}");
                        result.Recommendations.AddRange(accessibilityResult.Recommendations);
                        result.IsSuccessful = false;
                    }
                    else
                    {
                        result.Status = ConfigurationStatus.Valid;
                        result.Details = "WEBSITE_RUN_FROM_PACKAGE is set to a valid and accessible URL.";
                    }
                }
                else if (configuration.Mode == RunFromPackageMode.LocalPackage)
                {
                    // For local package mode, we can't verify the actual files but we can check if the value is correct
                    result.Status = ConfigurationStatus.Valid;
                    result.Details = "WEBSITE_RUN_FROM_PACKAGE is set to '1' for local package mode.";
                }
                else if (configuration.Mode == RunFromPackageMode.None)
                {
                    // No WEBSITE_RUN_FROM_PACKAGE set
                    result.Status = ConfigurationStatus.Missing;
                    result.Issues.Add("WEBSITE_RUN_FROM_PACKAGE setting is not set.");
                    result.Recommendations.Add($"Add WEBSITE_RUN_FROM_PACKAGE app setting with value '{skuCapabilities.RecommendedValue}'.");
                    result.IsSuccessful = false;
                }

                // Check if the configured value matches the recommended value for the SKU
                if (result.Status == ConfigurationStatus.Valid && 
                    configuration.CurrentValue != skuCapabilities.RecommendedValue && 
                    skuCapabilities.RecommendedMode != RunFromPackageMode.None)
                {
                    result.Status = ConfigurationStatus.Suboptimal;
                    result.Issues.Add($"The current value '{configuration.CurrentValue}' works but is not optimal for {configuration.OperatingSystem} {configuration.Sku}.");
                    result.Recommendations.Add($"Consider changing WEBSITE_RUN_FROM_PACKAGE to '{skuCapabilities.RecommendedValue}' for best performance.");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error verifying WEBSITE_RUN_FROM_PACKAGE configuration for {ResourceId}", resourceId);
                result.IsSuccessful = false;
                result.Status = ConfigurationStatus.Unknown;
                result.ErrorMessage = $"An error occurred during verification: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Validates that the package URL is accessible
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <param name="packageUrl">Optional package URL to validate. If not provided, the URL from app settings will be used</param>
        /// <returns>Result indicating accessibility of the package</returns>
        public async Task<PackageAccessibilityResult> ValidatePackageAccessibilityAsync(string resourceId, string packageUrl = "")
        {
            _logger.LogInternalInformation("Validating package accessibility for {ResourceId}", resourceId);

            // If packageUrl is not provided, retrieve it from app settings
            if (string.IsNullOrWhiteSpace(packageUrl))
            {
                var config = await GetRunFromPackageConfigurationAsync(resourceId);
                if (config.Mode != RunFromPackageMode.ExternalUrl)
                {
                    var result = new PackageAccessibilityResult
                    {
                        ResourceId = resourceId,
                        PackageUrl = SanitizeUrl(config.CurrentValue),
                        IsAccessible = false,
                        IsSuccessful = false,
                        ErrorDetails = $"WEBSITE_RUN_FROM_PACKAGE is not set to a URL. Current mode: {config.Mode}",
                    };
                    result.Recommendations.Add("Set WEBSITE_RUN_FROM_PACKAGE to a valid URL pointing to a zip file.");
                    return result;
                }
                
                packageUrl = config.CurrentValue;
            }

            // Use the secure validation method
            return await ValidateWithoutExposing(resourceId, packageUrl);
        }

        /// <summary>
        /// Inspects the package structure without exposing the contents
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <param name="packageUrl">Optional package URL to inspect. If not provided, the URL from app settings will be used</param>
        /// <returns>Package structure report with details but without exposing sensitive content</returns>
        public async Task<PackageStructureReport> InspectPackageStructureAsync(string resourceId, string packageUrl = "")
        {
            var report = new PackageStructureReport
            {
                ResourceId = resourceId,
                PackageUrl = packageUrl
            };

            try
            {
                _logger.LogInternalInformation("Starting package structure inspection for {ResourceId}", resourceId);

                // Get package URL if not provided
                string targetPackageUrl = packageUrl;
                if (string.IsNullOrWhiteSpace(targetPackageUrl))
                {
                    var config = await GetRunFromPackageConfigurationAsync(resourceId);
                    if (config.Mode == RunFromPackageMode.LocalPackage)
                    {
                        report.IsSuccessful = true;
                        report.ErrorMessage = "";
                        report.Details = "WEBSITE_RUN_FROM_PACKAGE is set to '1' (local package mode). Package structure cannot be inspected remotely as files are stored locally in the SitePackages folder.";
                        report.DetectedRuntime = "Unknown (Local Mode)";
                        report.HasValidStructure = true; // Assume valid since the app is configured
                        return report;
                    }

                    if (config.Mode != RunFromPackageMode.ExternalUrl || string.IsNullOrWhiteSpace(config.CurrentValue))
                    {
                        report.IsSuccessful = false;
                        report.ErrorMessage = "No valid package URL found in WEBSITE_RUN_FROM_PACKAGE setting";
                        return report;
                    }

                    targetPackageUrl = config.CurrentValue;
                }

                report.PackageUrl = SanitizeUrl(targetPackageUrl);

                // Download and inspect the package
                using var packageStream = await GetPackageStreamAsync(targetPackageUrl);
                if (packageStream == null)
                {
                    report.IsSuccessful = false;
                    report.ErrorMessage = "Failed to download package or package is empty. This could be due to network connectivity issues, authentication problems, or the package being unavailable.";
                    
                    // Add specific troubleshooting recommendations
                    report.Recommendations.Add("Verify the package URL is accessible and the blob exists");
                    report.Recommendations.Add("Check network connectivity and firewall settings");
                    report.Recommendations.Add("Ensure proper authentication is configured for blob storage access");
                    report.Recommendations.Add("Verify SAS token is valid and not expired if using external URL");
                    
                    return report;
                }

                // Check package size
                long packageSize = 0;
                try
                {
                    // Try to access Length directly, even if CanSeek returns true
                    // RetriableStream can report CanSeek=true but still throw NotSupportedException on Length
                    packageSize = packageStream.Length;
                }
                catch (NotSupportedException)
                {
                    // If Length is not supported (e.g., RetriableStream from Azure SDK), 
                    // try to get size from blob properties as fallback
                    packageSize = await GetPackageSizeFromPropertiesAsync(targetPackageUrl);
                }
                
                if (packageSize > 0)
                {
                    report.PackageSize = packageSize;
                    if (!IsPackageSizeAcceptable(packageSize))
                    {
                        report.IsSuccessful = false;
                        report.ErrorMessage = $"Package size ({FormatSize(packageSize)}) exceeds maximum allowed size (500 MB)";
                        return report;
                    }
                }

                // Inspect ZIP structure
                await InspectZipStructureAsync(packageStream, report);

                report.IsSuccessful = true;
                _logger.LogInternalInformation("Package structure inspection completed for {ResourceId}", resourceId);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error inspecting package structure for {ResourceId}", resourceId);
                report.IsSuccessful = false;
                
                // Categorize the error and provide specific guidance
                if (ex is HttpRequestException httpEx)
                {
                    if (httpEx.Message.Contains("connection was aborted") || httpEx.Message.Contains("host machine"))
                    {
                        report.ErrorMessage = "Network connectivity issue occurred while downloading the package. The connection was aborted by the host machine.";
                        report.Recommendations.Add("Check network connectivity and retry the operation");
                        report.Recommendations.Add("Verify firewall settings are not blocking the connection");
                        report.Recommendations.Add("Consider using a different network or checking with network administrators");
                    }
                    else if (httpEx.Message.Contains("timeout"))
                    {
                        report.ErrorMessage = "Timeout occurred while downloading the package.";
                        report.Recommendations.Add("The package may be large or the network connection slow");
                        report.Recommendations.Add("Retry the operation when network conditions are better");
                    }
                    else
                    {
                        report.ErrorMessage = $"HTTP error occurred during package download: {httpEx.Message}";
                        report.Recommendations.Add("Verify the package URL is accessible");
                        report.Recommendations.Add("Check authentication credentials if required");
                    }
                }
                else if (ex is TaskCanceledException)
                {
                    report.ErrorMessage = "Package download operation timed out.";
                    report.Recommendations.Add("The package may be large or network connection is slow");
                    report.Recommendations.Add("Retry the operation later");
                }
                else if (ex is SocketException || ex.InnerException is SocketException)
                {
                    report.ErrorMessage = "Network socket error occurred during package download.";
                    report.Recommendations.Add("Check network connectivity");
                    report.Recommendations.Add("Verify DNS resolution is working properly");
                    report.Recommendations.Add("Check for any proxy or firewall issues");
                }
                else
                {
                    report.ErrorMessage = $"An error occurred during package inspection: {ex.Message}";
                    report.Recommendations.Add("Verify the package URL and accessibility");
                    report.Recommendations.Add("Check logs for more detailed error information");
                }
            }

            return report;
        }

        /// <summary>
        /// Performs a comprehensive diagnosis of the WEBSITE_RUN_FROM_PACKAGE configuration
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <returns>Detailed diagnostic report</returns>
        public async Task<RunFromPackageDiagnosticReport> DiagnoseRunFromPackageIssuesAsync(string resourceId)
        {
            // This is a placeholder implementation that will be expanded in future phases
            var report = new RunFromPackageDiagnosticReport
            {
                ResourceId = resourceId,
                IsSuccessful = true
            };

            try
            {
                // Get the current configuration
                var config = await GetRunFromPackageConfigurationAsync(resourceId);
                report.Configuration = config;
                report.Sku = config.Sku;
                report.OperatingSystem = config.OperatingSystem;

                // Get SKU capabilities
                var skuCapabilities = SkuCapabilities.GetForSku(config.Sku, config.OperatingSystem);
                report.SkuSupportsFeature = skuCapabilities.SupportsMode(config.Mode);

                // Verify the current configuration
                var verificationResult = await VerifyRunFromPackageConfigurationAsync(resourceId);

                // Map verification issues to diagnostic issues
                if (verificationResult.Status == ConfigurationStatus.Missing)
                {
                    report.ConfigurationIssues.Add(new Models.RunFromPackage.DiagnosticIssue
                    {
                        Type = IssueType.Configuration,
                        Severity = IssueSeverity.High,
                        Description = "WEBSITE_RUN_FROM_PACKAGE setting is missing",
                        RecommendedActions = new List<string> { $"Add WEBSITE_RUN_FROM_PACKAGE app setting with value '{skuCapabilities.RecommendedValue}'." },
                        IsFixable = true,
                        FixAction = RunFromPackageRepairAction.SetRecommendedValue
                    });
                    
                    report.OverallStatus = HealthStatus.Unhealthy;
                }
                else if (verificationResult.Status == ConfigurationStatus.Invalid)
                {
                    report.ConfigurationIssues.Add(new Models.RunFromPackage.DiagnosticIssue
                    {
                        Type = IssueType.Configuration,
                        Severity = IssueSeverity.Critical,
                        Description = $"WEBSITE_RUN_FROM_PACKAGE has an invalid value: {config.CurrentValue}.",
                        RecommendedActions = verificationResult.Recommendations,
                        IsFixable = true,
                        FixAction = RunFromPackageRepairAction.SetRecommendedValue
                    });
                    
                    report.OverallStatus = HealthStatus.Unhealthy;
                }
                else if (verificationResult.Status == ConfigurationStatus.Unsupported)
                {
                    report.ConfigurationIssues.Add(new Models.RunFromPackage.DiagnosticIssue
                    {
                        Type = IssueType.SkuCompatibility,
                        Severity = IssueSeverity.High,
                        Description = $"The current value '{config.CurrentValue}' is not supported for {config.OperatingSystem} {config.Sku}.",
                        RecommendedActions = verificationResult.Recommendations,
                        IsFixable = true,
                        FixAction = RunFromPackageRepairAction.SetRecommendedValue
                    });
                    
                    report.OverallStatus = HealthStatus.Unhealthy;
                }
                else if (verificationResult.Status == ConfigurationStatus.Suboptimal)
                {
                    report.ConfigurationIssues.Add(new Models.RunFromPackage.DiagnosticIssue
                    {
                        Type = IssueType.Configuration,
                        Severity = IssueSeverity.Medium,
                        Description = $"The current value '{config.CurrentValue}' works but is not optimal for {config.OperatingSystem} {config.Sku}.",
                        RecommendedActions = verificationResult.Recommendations,
                        IsFixable = true,
                        FixAction = RunFromPackageRepairAction.SetRecommendedValue
                    });
                    
                    report.OverallStatus = HealthStatus.Degraded;
                }
                if (config.Mode == RunFromPackageMode.ExternalUrl)
                {
                    // Check accessibility for external URL mode
                    var accessibilityResult = await ValidateWithoutExposing(resourceId, config.CurrentValue);
                    
                    if (!accessibilityResult.IsAccessible)
                    {
                        report.StorageIssues.Add(new Models.RunFromPackage.DiagnosticIssue
                        {
                            Type = IssueType.Storage,
                            Severity = IssueSeverity.Critical,
                            Description = $"The package URL is not accessible: {accessibilityResult.ErrorDetails}",
                            RecommendedActions = accessibilityResult.Recommendations,
                            IsFixable = true,
                            FixAction = accessibilityResult.RequiresAuthentication ? 
                                RunFromPackageRepairAction.GenerateSasUrl : 
                                RunFromPackageRepairAction.UpdateUrl
                        });
                        
                        report.OverallStatus = HealthStatus.Critical;
                    }
                    else
                    {
                        // Package is accessible, now check its structure
                        // Pass the package URL to avoid duplicate GetRunFromPackageConfigurationAsync call
                        var structureReport = await InspectPackageStructureAsync(resourceId, config.CurrentValue);
                        
                        if (!structureReport.IsSuccessful)
                        {
                            report.ConfigurationIssues.Add(new Models.RunFromPackage.DiagnosticIssue
                            {
                                Type = IssueType.Configuration,
                                Severity = IssueSeverity.High,
                                Description = $"Failed to inspect package structure: {structureReport.ErrorMessage}",
                                RecommendedActions = new List<string> { "Verify the package is a valid ZIP file", "Check package accessibility" },
                                IsFixable = false
                            });
                            
                            report.OverallStatus = HealthStatus.Degraded;
                        }
                        else if (!structureReport.HasValidStructure)
                        {
                            // Add structure issues to the report
                            foreach (var issue in structureReport.StructureIssues)
                            {
                                report.ConfigurationIssues.Add(new Models.RunFromPackage.DiagnosticIssue
                                {
                                    Type = IssueType.Configuration,
                                    Severity = IssueSeverity.High,
                                    Description = $"Package structure issue: {issue}",
                                    RecommendedActions = structureReport.Recommendations,
                                    IsFixable = true
                                });
                            }
                            
                            // Add missing files issues
                            foreach (var missingFile in structureReport.MissingRequiredFiles)
                            {
                                report.ConfigurationIssues.Add(new Models.RunFromPackage.DiagnosticIssue
                                {
                                    Type = IssueType.Configuration,
                                    Severity = IssueSeverity.High,
                                    Description = $"Missing required file: {missingFile}",
                                    RecommendedActions = new List<string> { $"Add {missingFile} to the package root directory" },
                                    IsFixable = true
                                });
                            }
                            
                            report.OverallStatus = HealthStatus.Degraded;
                        }
                        else
                        {
                            report.OverallStatus = HealthStatus.Healthy;
                        }
                    }
                }
                else if (config.Mode == RunFromPackageMode.LocalPackage)
                {
                    // For local package mode
                    report.OverallStatus = HealthStatus.Healthy;
                }
                else if (config.Mode == RunFromPackageMode.Invalid || config.Mode == RunFromPackageMode.None)
                {
                    // Invalid configurations should already be handled above, but make sure we don't set to Healthy
                    if (report.OverallStatus == HealthStatus.Unknown)
                    {
                        report.OverallStatus = HealthStatus.Unhealthy;
                    }
                }
                else
                {
                    // Other valid configurations (shouldn't reach here normally)
                    if (report.OverallStatus == HealthStatus.Unknown)
                    {
                        report.OverallStatus = HealthStatus.Healthy;
                    }
                }

                // Generate summary based on overall status
                report.Summary = report.OverallStatus switch
                {
                    HealthStatus.Healthy => "The WEBSITE_RUN_FROM_PACKAGE configuration is healthy and properly configured.",
                    HealthStatus.Degraded => "The WEBSITE_RUN_FROM_PACKAGE configuration is functional but could be optimized.",
                    HealthStatus.Unhealthy => "The WEBSITE_RUN_FROM_PACKAGE configuration has issues that should be addressed.",
                    HealthStatus.Critical => "The WEBSITE_RUN_FROM_PACKAGE configuration has critical issues that must be fixed.",
                    _ => "The WEBSITE_RUN_FROM_PACKAGE configuration status cannot be determined."
                };

                // Add recommendations based on all issues
                var allIssues = report.ConfigurationIssues
                    .Concat(report.StorageIssues)
                    .Concat(report.NetworkIssues)
                    .Concat(report.PermissionIssues)
                    .ToList();

                foreach (var issue in allIssues)
                {
                    report.Recommendations.AddRange(issue.RecommendedActions);
                }

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error diagnosing WEBSITE_RUN_FROM_PACKAGE issues for {ResourceId}", resourceId);
                report.IsSuccessful = false;
                report.ErrorMessage = $"An error occurred while diagnosing WEBSITE_RUN_FROM_PACKAGE issues: {ex.Message}";
                report.OverallStatus = HealthStatus.Unknown;
                return report;
            }
        }

        /// <summary>
        /// Gets metadata about the package without retrieving its contents
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <param name="packageUrl">Optional package URL to get metadata for. If not provided, the URL from app settings will be used</param>
        /// <returns>Package metadata</returns>
        public async Task<PackageMetadata> GetPackageMetadataAsync(string resourceId, string packageUrl = "")
        {
            var metadata = new PackageMetadata();

            try
            {
                _logger.LogInternalInformation("Getting package metadata for {ResourceId}", resourceId);

                // Get package URL if not provided
                string targetPackageUrl = packageUrl;
                if (string.IsNullOrWhiteSpace(targetPackageUrl))
                {
                    var config = await GetRunFromPackageConfigurationAsync(resourceId);
                    if (config.Mode == RunFromPackageMode.LocalPackage)
                    {
                        metadata.IsSuccessful = true;
                        metadata.PackageUrl = "Local Package Mode";
                        metadata.StorageAccount = "Local SitePackages folder";
                        metadata.ContainerName = "N/A";
                        metadata.Size = 0;
                        return metadata;
                    }

                    if (config.Mode != RunFromPackageMode.ExternalUrl || string.IsNullOrWhiteSpace(config.CurrentValue))
                    {
                        metadata.IsSuccessful = false;
                        metadata.ErrorMessage = "No valid package URL found in WEBSITE_RUN_FROM_PACKAGE setting";
                        return metadata;
                    }

                    targetPackageUrl = config.CurrentValue;
                }

                metadata.PackageUrl = SanitizeUrl(targetPackageUrl);

                // Parse URL to get storage account and container info
                if (Uri.TryCreate(targetPackageUrl, UriKind.Absolute, out Uri? packageUri))
                {
                    if (TryParseBlobUri(packageUri, out string accountName, out string containerName, out string blobPath))
                    {
                        metadata.StorageAccount = accountName;
                        metadata.ContainerName = containerName;

                        // Use blob storage client to get metadata
                        try
                        {
                            var properties = await _blobStorageClient.GetBlobPropertiesAsync(containerName, blobPath, CancellationToken.None);
                            metadata.Size = properties.ContentLength;
                            metadata.LastModified = properties.LastModified.DateTime;
                            metadata.ContentType = properties.ContentType;
                            metadata.ETag = properties.ETag.ToString();
                            metadata.IsSuccessful = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInternalError(ex, "Error getting blob properties for {BlobPath}", blobPath);
                            
                            // Fallback to HTTP HEAD request
                            await GetMetadataViaHttpAsync(targetPackageUrl, metadata);
                        }
                    }
                    else
                    {
                        // Non-Azure blob URL, use HTTP HEAD request
                        await GetMetadataViaHttpAsync(targetPackageUrl, metadata);
                    }
                }
                else
                {
                    metadata.IsSuccessful = false;
                    metadata.ErrorMessage = "Invalid package URL format";
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error getting package metadata for {ResourceId}", resourceId);
                metadata.IsSuccessful = false;
                metadata.ErrorMessage = $"An error occurred while retrieving package metadata: {ex.Message}";
            }

            return metadata;
        }

        /// <summary>
        /// Repairs the WEBSITE_RUN_FROM_PACKAGE configuration
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <param name="repairAction">The type of repair action to perform</param>
        /// <param name="newValue">Optional new value for the setting</param>
        /// <returns>Result of the repair operation</returns>
        public async Task<RunFromPackageRepairResult> RepairRunFromPackageConfigurationAsync(
            string resourceId, 
            RunFromPackageRepairAction repairAction, 
            string newValue = "")
        {
            var result = new RunFromPackageRepairResult
            {
                ResourceId = resourceId,
                RepairAction = repairAction,
                IsSuccessful = false,
                ErrorMessage = ""
            };

            try
            {
                _logger.LogInternalInformation("Repairing WEBSITE_RUN_FROM_PACKAGE configuration for {ResourceId} with action {RepairAction}", 
                    resourceId, repairAction);

                // Determine the value to set based on the repair action
                string targetValue = repairAction switch
                {
                    RunFromPackageRepairAction.SetToOne => "1",
                    RunFromPackageRepairAction.UpdateUrl => newValue,
                    RunFromPackageRepairAction.RemoveSetting => "",
                    _ => ""
                };

                // Build the app settings update
                if (repairAction == RunFromPackageRepairAction.RemoveSetting)
                {
                    // For removal, we need to get existing settings and remove the specific key
                    string appSettingsJson = await _armHelper.GetAppSettings(resourceId);
                    if (string.IsNullOrWhiteSpace(appSettingsJson))
                    {
                        result.ErrorMessage = "Failed to retrieve existing app settings for removal";
                        _logger.LogInternalError("Failed to retrieve app settings for removal for {ResourceId}", resourceId);
                        return result;
                    }

                    var appSettings = JObject.Parse(appSettingsJson);
                    var properties = appSettings["properties"] as JObject;
                    var existingSettings = properties?.ToObject<Dictionary<string, string>>() ?? new Dictionary<string, string>();

                    // Remove the WEBSITE_RUN_FROM_PACKAGE setting
                    existingSettings.Remove("WEBSITE_RUN_FROM_PACKAGE");

                    // Use UpdateAppSettingsAsync to update with the setting removed
                    bool updateSuccess = await _armHelper.UpdateAppSettingsAsync(resourceId, existingSettings);
                    result.IsSuccessful = updateSuccess;
                    if (updateSuccess)
                    {
                        result.NewValue = "";
                        _logger.LogInternalInformation("Successfully removed WEBSITE_RUN_FROM_PACKAGE for {ResourceId}", resourceId);
                    }
                    else
                    {
                        result.ErrorMessage = "Failed to remove WEBSITE_RUN_FROM_PACKAGE setting";
                        _logger.LogInternalError("Failed to remove WEBSITE_RUN_FROM_PACKAGE for {ResourceId}", resourceId);
                    }
                }
                else
                {
                    // For set/update operations, use the helper method
                    var updateSettings = new Dictionary<string, string>
                    {
                        { "WEBSITE_RUN_FROM_PACKAGE", targetValue }
                    };

                    bool updateSuccess = await _armHelper.UpdateAppSettingsAsync(resourceId, updateSettings);
                    result.IsSuccessful = updateSuccess;
                    if (updateSuccess)
                    {
                        result.NewValue = targetValue;
                        _logger.LogInternalInformation("Successfully updated WEBSITE_RUN_FROM_PACKAGE for {ResourceId} to {NewValue}", 
                            resourceId, targetValue);
                    }
                    else
                    {
                        result.ErrorMessage = "Failed to update WEBSITE_RUN_FROM_PACKAGE setting";
                        _logger.LogInternalError("Failed to update WEBSITE_RUN_FROM_PACKAGE for {ResourceId} to {NewValue}", 
                            resourceId, targetValue);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error repairing WEBSITE_RUN_FROM_PACKAGE configuration for {ResourceId}", resourceId);
                result.ErrorMessage = $"An error occurred during repair: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Generates a temporary SAS URL for accessing a package
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <param name="storageAccountName">The name of the storage account</param>
        /// <param name="containerName">The name of the container</param>
        /// <param name="blobName">The name of the blob</param>
        /// <param name="expiryHours">Number of hours until the SAS expires</param>
        /// <returns>Result containing the SAS URL (with no exposed secrets)</returns>
        public async Task<SasUrlGenerationResult> GeneratePackageSasUrlAsync(
            string resourceId,
            string storageAccountName,
            string containerName,
            string blobName,
            int expiryHours = 24)
        {
            _logger.LogInternalInformation("Generating secure SAS URL for {ResourceId}", resourceId);
            
            // Use the secure implementation with limited expiry time
            var result = await GenerateSecureSasTokenAsync(resourceId, storageAccountName, containerName, blobName);
            
            // Override expiry if different from default (1 hour)
            if (expiryHours != 1 && result.IsSuccessful)
            {
                _logger.LogInternalInformation("Custom expiry requested: {ExpiryHours} hours", expiryHours);
                // For custom expiry, we'd need to regenerate with different expiration
                // For now, log the request but keep the secure 1-hour default
            }
            
            return result;
        }

        /// <summary>
        /// Migrates from an external URL to local package mode
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <returns>Result of the migration operation</returns>
        public async Task<MigrationResult> MigrateToLocalPackageAsync(string resourceId)
        {
            // This is a placeholder implementation that will be expanded in future phases
            var result = new MigrationResult
            {
                IsSuccessful = false,
                ErrorMessage = "Migration functionality not yet implemented."
            };

            return await Task.FromResult(result);
        }
        
        /// <summary>
        /// Checks if the resource has issues with WEBSITE_RUN_FROM_PACKAGE configuration
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <returns>True if issues are detected, otherwise false</returns>
        public async Task<bool> HasRunFromPackageIssuesAsync(string resourceId)
        {
            try
            {
                var verificationResult = await VerifyRunFromPackageConfigurationAsync(resourceId);
                
                // Consider any status other than Valid as having issues
                bool hasIssues = verificationResult.Status != ConfigurationStatus.Valid;
                
                // Use configuration from verification result to avoid duplicate call
                var configuration = verificationResult.Configuration;
                if (configuration == null)
                {
                    _logger.LogInternalError("Configuration is null in verification result for {ResourceId}", resourceId);
                    return true; // Return true if we can't get configuration
                }
                
                // Use SKU capabilities from verification result to avoid duplicate call
                var skuCapabilities = verificationResult.SkuCapabilities;
                if (skuCapabilities == null)
                {
                    _logger.LogInternalError("SkuCapabilities is null in verification result for {ResourceId}", resourceId);
                    return true; // Return true if we can't get capabilities
                }
                
                // Always check package structure if:
                // 1. Basic configuration is valid, OR
                // 2. Mode is ExternalUrl and recommendation is to use local package ("1")
                bool shouldCheckPackageStructure = !hasIssues || 
                    (configuration.Mode == RunFromPackageMode.ExternalUrl && skuCapabilities.RecommendedValue == "1");
                
                if (shouldCheckPackageStructure)
                {
                    _logger.LogInternalInformation("Checking package structure for {ResourceId} (hasIssues: {HasIssues}, mode: {Mode}, recommended: {Recommended})", 
                        resourceId, hasIssues, configuration.Mode, skuCapabilities.RecommendedValue);
                    
                    // Pass the package URL to avoid duplicate GetRunFromPackageConfigurationAsync call
                    string packageUrl = configuration.Mode == RunFromPackageMode.ExternalUrl ? configuration.CurrentValue : "";
                    var structureReport = await InspectPackageStructureAsync(resourceId, packageUrl);
                    
                    // Check if package structure inspection revealed any issues
                    if (!structureReport.IsSuccessful)
                    {
                        _logger.LogInternalInformation("Package structure inspection failed for {ResourceId}: {Error}", resourceId, structureReport.ErrorMessage);
                        hasIssues = true;
                    }
                    else if (!structureReport.HasValidStructure)
                    {
                        _logger.LogInternalInformation("Package structure validation failed for {ResourceId}. Issues: {Issues}", 
                            resourceId, string.Join(", ", structureReport.StructureIssues));
                        hasIssues = true;
                    }
                    else if (structureReport.MissingRequiredFiles.Count > 0)
                    {
                        _logger.LogInternalInformation("Package missing required files for {ResourceId}: {MissingFiles}", 
                            resourceId, string.Join(", ", structureReport.MissingRequiredFiles));
                        hasIssues = true;
                    }
                    else
                    {
                        _logger.LogInternalInformation("Package structure validation passed for {ResourceId}", resourceId);
                    }
                }
                
                return hasIssues;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error checking for WEBSITE_RUN_FROM_PACKAGE issues for {ResourceId}", resourceId);
                // Return true if we can't determine the status, to be safe
                return true;
            }
        }

        /// <summary>
        /// Gets the SKU and capabilities related to WEBSITE_RUN_FROM_PACKAGE for a resource
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <returns>The SKU and supported WEBSITE_RUN_FROM_PACKAGE configurations</returns>
        public async Task<SkuCapabilities> GetSkuCapabilitiesAsync(string resourceId)
        {
            try
            {
                // Get the SKU name and OS
                string skuName = await GetSkuViaHttpAsync(resourceId);
                string operatingSystem = await _armHelper.GetOperatingSystemAsync(resourceId);
                
                // Get the capabilities for this SKU
                var capabilities = SkuCapabilities.GetForSku(skuName, operatingSystem);
                
                return capabilities;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error getting SKU capabilities for {ResourceId}", resourceId);
                
                // Return default capabilities if we can't determine the SKU
                return new SkuCapabilities
                {
                    SkuName = "Unknown",
                    OperatingSystem = "Unknown",
                    SupportsLocalPackage = true,
                    SupportsExternalUrl = true,
                    RecommendedMode = RunFromPackageMode.LocalPackage,
                    RecommendedValue = "1",
                    Details = "Using default capabilities because the SKU could not be determined."
                };
            }
        }

        /// <summary>
        /// Retrieves a summary of recommended WEBSITE_RUN_FROM_PACKAGE configuration
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <returns>A summary of recommended configurations based on the SKU</returns>
        public async Task<string> GetRunFromPackageRecommendationsAsync(string resourceId)
        {
            try
            {
                // Get the current configuration
                var config = await GetRunFromPackageConfigurationAsync(resourceId);
                
                // Get the capabilities for this SKU
                var capabilities = await GetSkuCapabilitiesAsync(resourceId);
                
                // Build the recommendations
                var recommendationsBuilder = new System.Text.StringBuilder();
                
                recommendationsBuilder.AppendLine($"Recommendations for {config.ResourceType} [{config.OperatingSystem} {capabilities.SkuName}]:");
                recommendationsBuilder.AppendLine();
                
                recommendationsBuilder.AppendLine($"Current WEBSITE_RUN_FROM_PACKAGE value: {config.CurrentValue}");
                recommendationsBuilder.AppendLine($"Current mode: {config.Mode}");
                recommendationsBuilder.AppendLine();
                
                recommendationsBuilder.AppendLine("Supported modes:");
                recommendationsBuilder.AppendLine($"- Local Package Mode ('1'): {(capabilities.SupportsLocalPackage ? "Supported" : "Not Supported")}");
                recommendationsBuilder.AppendLine($"- External URL Mode: {(capabilities.SupportsExternalUrl ? "Supported" : "Not Supported")}");
                recommendationsBuilder.AppendLine();
                
                recommendationsBuilder.AppendLine($"Recommended value: {capabilities.RecommendedValue}");
                recommendationsBuilder.AppendLine();
                
                if (!string.IsNullOrEmpty(capabilities.Details))
                {
                    recommendationsBuilder.AppendLine("Details:");
                    recommendationsBuilder.AppendLine(capabilities.Details);
                    recommendationsBuilder.AppendLine();
                }
                
                if (capabilities.Limitations.Count > 0)
                {
                    recommendationsBuilder.AppendLine("Limitations:");
                    foreach (var limitation in capabilities.Limitations)
                    {
                        recommendationsBuilder.AppendLine($"- {limitation}");
                    }
                    recommendationsBuilder.AppendLine();
                }
                
                return recommendationsBuilder.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error getting WEBSITE_RUN_FROM_PACKAGE recommendations for {ResourceId}", resourceId);
                return $"Error getting recommendations: {ex.Message}";
            }
        }

        /// <summary>
        /// Gets the SKU for a Function App or Web App
        /// </summary>
        /// <summary>
        /// Gets the operating system for a resource using HTTP calls (for better testability)
        /// </summary>
        /// <param name="resourceId">The Azure resource ID</param>
        /// <returns>The operating system (Windows or Linux)</returns>
        private async Task<string> GetOperatingSystemViaHttpAsync(string resourceId)
        {
            try
            {
                var requestUrl = $"https://management.azure.com{resourceId}?api-version=2022-03-01";
                var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                HttpResponseMessage responseMessage = await httpClient.SendAsync(request);

                if (!responseMessage.IsSuccessStatusCode)
                {
                    _logger.LogInternalWarning($"Failed to fetch resource details for {resourceId}: {responseMessage.ReasonPhrase}");
                    return "Windows"; // Default to Windows if we can't determine
                }

                var resourceJson = await responseMessage.Content.ReadAsStringAsync();
                var resource = JObject.Parse(resourceJson);
                var properties = resource["properties"] as JObject;
                
                if (properties != null)
                {
                    // Check siteConfig.linuxFxVersion for Linux apps
                    var siteConfig = properties["siteConfig"] as JObject;
                    if (siteConfig != null)
                    {
                        var linuxFxVersion = siteConfig["linuxFxVersion"]?.ToString();
                        if (!string.IsNullOrEmpty(linuxFxVersion))
                        {
                            return "Linux";
                        }
                    }

                    // Check kind property for Linux apps  
                    var kind = properties["kind"]?.ToString();
                    if (!string.IsNullOrEmpty(kind) && kind.Contains("linux", StringComparison.OrdinalIgnoreCase))
                    {
                        return "Linux";
                    }
                }

                // Default to Windows
                return "Windows";
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error getting operating system for {ResourceId}", resourceId);
                return "Windows"; // Default to Windows on error
            }
        }

        /// <summary>
        /// Gets the SKU name for the specified resource using HTTP calls (for better testability)
        /// </summary>
        /// <param name="resourceId">The Azure resource ID</param>
        /// <returns>The SKU name</returns>
        private async Task<string> GetSkuViaHttpAsync(string resourceId)
        {
            try
            {
                // First get the App Service Plan ID for this resource
                var appServicePlanId = await GetAppServicePlanIdViaHttpAsync(resourceId);
                
                if (string.IsNullOrWhiteSpace(appServicePlanId))
                {
                    _logger.LogInternalWarning("No App Service Plan ID found for resource {ResourceId}", resourceId);
                    return "Unknown";
                }

                // Get the SKU details for this App Service Plan
                var requestUrl = $"https://management.azure.com{appServicePlanId}?api-version=2022-03-01";
                var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                HttpResponseMessage responseMessage = await httpClient.SendAsync(request);

                if (!responseMessage.IsSuccessStatusCode)
                {
                    _logger.LogInternalWarning($"Failed to fetch App Service Plan details for {appServicePlanId}: {responseMessage.ReasonPhrase}");
                    return "Unknown";
                }

                var planJson = await responseMessage.Content.ReadAsStringAsync();
                var plan = JObject.Parse(planJson);
                var sku = plan["sku"] as JObject;
                
                if (sku != null)
                {
                    // Use tier for SKU capability matching as it contains semantic names like "ElasticPremium"
                    // rather than just the size code like "EP1"
                    var skuTier = sku["tier"]?.ToString();
                    if (!string.IsNullOrEmpty(skuTier))
                    {
                        _logger.LogInternalInformation("Retrieved SKU tier {SkuTier} for resource {ResourceId}", skuTier, resourceId);
                        return skuTier;
                    }
                    
                    // Fallback to name if tier is not available
                    var skuName = sku["name"]?.ToString();
                    if (!string.IsNullOrEmpty(skuName))
                    {
                        _logger.LogInternalInformation("Retrieved SKU name {SkuName} for resource {ResourceId} (tier not available)", skuName, resourceId);
                        return skuName;
                    }
                }

                _logger.LogInternalWarning("No valid SKU information found for App Service Plan {AppServicePlanId}", appServicePlanId);
                return "Unknown";
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error getting SKU for {ResourceId}", resourceId);
                return "Unknown";
            }
        }

        /// <summary>
        /// Gets the App Service Plan ID for a resource using HTTP calls
        /// </summary>
        /// <param name="resourceId">The Azure resource ID</param>
        /// <returns>The App Service Plan ID</returns>
        private async Task<string> GetAppServicePlanIdViaHttpAsync(string resourceId)
        {
            try
            {
                var requestUrl = $"https://management.azure.com{resourceId}?api-version=2022-03-01";
                var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                HttpResponseMessage responseMessage = await httpClient.SendAsync(request);

                if (!responseMessage.IsSuccessStatusCode)
                {
                    _logger.LogInternalWarning($"Failed to fetch resource details for App Service Plan ID lookup for {resourceId}: {responseMessage.ReasonPhrase}");
                    return string.Empty;
                }

                var resourceJson = await responseMessage.Content.ReadAsStringAsync();
                var resource = JObject.Parse(resourceJson);
                var properties = resource["properties"] as JObject;
                
                if (properties != null)
                {
                    var serverFarmId = properties["serverFarmId"]?.ToString();
                    return serverFarmId ?? string.Empty;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error getting App Service Plan ID for {ResourceId}", resourceId);
                return string.Empty;
            }
        }

        /// <summary>
        /// Attempts to parse a blob URI into account name, container name, and blob path
        /// </summary>
        private bool TryParseBlobUri(Uri uri, out string accountName, out string containerName, out string blobPath)
        {
            accountName = string.Empty;
            containerName = string.Empty;
            blobPath = string.Empty;

            try
            {
                // Handle different URL patterns
                if (uri.Host.EndsWith(".blob.core.windows.net"))
                {
                    // Standard blob URL: https://{account}.blob.core.windows.net/{container}/{blob}
                    accountName = uri.Host.Split('.')[0];

                    // Split the path parts, removing the leading slash
                    var pathParts = uri.AbsolutePath.TrimStart('/').Split('/', 2);
                    if (pathParts.Length >= 2)
                    {
                        containerName = pathParts[0];
                        blobPath = pathParts[1];
                        return true;
                    }
                }
                else if (uri.Host.EndsWith(".blob.storage.azure.net") ||
                         uri.Host.Contains(".blob.storage."))
                {
                    // Custom domain or regional endpoint: https://{account}.blob.storage.azure.net/{container}/{blob}
                    // or https://{account}.blob.storage.{region}.azure.net/{container}/{blob}
                    var hostParts = uri.Host.Split('.');
                    accountName = hostParts[0];

                    // Split the path parts, removing the leading slash
                    var pathParts = uri.AbsolutePath.TrimStart('/').Split('/', 2);
                    if (pathParts.Length >= 2)
                    {
                        containerName = pathParts[0];
                        blobPath = pathParts[1];
                        return true;
                    }
                }
                else if (uri.Host == "core.windows.net" ||
                         uri.Host.EndsWith(".core.windows.net"))
                {
                    // SAS token URL: https://core.windows.net/{account}/{container}/{blob}?{sas}
                    // or https://{region}.core.windows.net/{account}/{container}/{blob}?{sas}
                    var pathParts = uri.AbsolutePath.TrimStart('/').Split('/', 3);
                    if (pathParts.Length >= 3)
                    {
                        accountName = pathParts[0];
                        containerName = pathParts[1];
                        blobPath = pathParts[2];
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Verifies files in a blob container.
        /// Handles both URL-based WEBSITE_RUN_FROM_PACKAGE (external blob storage) and local package mode (value "1").
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <param name="containerPath">Optional path to the blob container. If not provided, the WEBSITE_RUN_FROM_PACKAGE app setting will be parsed to extract container information</param>
        /// <returns>A verification result containing the list of files in the container, or confirmation of local package mode if WEBSITE_RUN_FROM_PACKAGE is set to "1"</returns>
        public async Task<BlobContainerVerificationResult> VerifyFilesInBlobContainerAsync(string resourceId, string containerPath = "")
        {
            var result = new BlobContainerVerificationResult();
            string targetFileName = string.Empty;
            string targetFilePath = string.Empty;

            try
            {
                _logger.LogInternalInformation("Verifying files in blob container for {ResourceId}, ContainerPath: {ContainerPath}",
                    resourceId, containerPath ?? "Not provided");

                // If containerPath is not provided, retrieve it from app settings
                if (string.IsNullOrWhiteSpace(containerPath))
                {
                    _logger.LogInternalInformation("Container path not provided. Attempting to extract from WEBSITE_RUN_FROM_PACKAGE app setting");

                    // Get app settings
                    string appSettingsJson = await _armHelper.GetAppSettings(resourceId);

                    if (string.IsNullOrWhiteSpace(appSettingsJson))
                    {
                        result.IsSuccessful = false;
                        result.ErrorMessage = "Failed to retrieve app settings";
                        result.FilesFound = false;
                        result.TargetFileFound = false;
                        return result;
                    }

                    // Parse app settings to get WEBSITE_RUN_FROM_PACKAGE value
                    var appSettings = JObject.Parse(appSettingsJson);
                    var properties = appSettings["properties"] as JObject;

                    if (properties == null || !properties.TryGetValue("WEBSITE_RUN_FROM_PACKAGE", StringComparison.OrdinalIgnoreCase, out var runFromPackageValue))
                    {
                        result.IsSuccessful = false;
                        result.ErrorMessage = "WEBSITE_RUN_FROM_PACKAGE app setting not found";
                        result.FilesFound = false;
                        result.TargetFileFound = false;
                        return result;
                    }

                    string zipFilePath = runFromPackageValue?.ToString() ?? string.Empty;

                    // Handle the case where WEBSITE_RUN_FROM_PACKAGE is set to "1" (local package mode)
                    if (string.IsNullOrWhiteSpace(zipFilePath) || zipFilePath == "0" || zipFilePath == "true")
                    {
                        result.IsSuccessful = false;
                        result.ErrorMessage = $"WEBSITE_RUN_FROM_PACKAGE has an invalid value: {zipFilePath}. Expected either '1' for local package mode or a URL to a zip file.";
                        result.FilesFound = false;
                        result.TargetFileFound = false;
                        return result;
                    }

                    // If WEBSITE_RUN_FROM_PACKAGE is set to "1", it indicates local package mode
                    // In this case, we cannot verify files in a blob container since files are stored locally
                    if (zipFilePath == "1")
                    {
                        result.IsSuccessful = true;
                        result.Details = "WEBSITE_RUN_FROM_PACKAGE is set to '1' (local package mode). Files are stored locally in the SitePackages folder and cannot be verified through blob container listing. This is a valid configuration for function apps.";
                        result.FilesFound = true; // We assume files exist since the app is configured for local package mode
                        result.TargetFileFound = false; // No specific target file to verify in local mode
                        result.TargetFilePath = "Local package mode (d:\\home\\data\\SitePackages or /home/data/SitePackages)";
                        return result;
                    }

                    // Store the target file path from the app setting
                    targetFilePath = zipFilePath;

                    // Extract the target file name from the path
                    if (targetFilePath.Contains('/'))
                    {
                        targetFileName = targetFilePath.Substring(targetFilePath.LastIndexOf('/') + 1);
                    }
                    else
                    {
                        targetFileName = targetFilePath;
                    }

                    // Set the target file path in the result
                    result.TargetFilePath = targetFilePath;

                    // Extract the container URL from the zip file path
                    if (!Uri.TryCreate(zipFilePath, UriKind.Absolute, out Uri? uri))
                    {
                        result.IsSuccessful = false;
                        result.ErrorMessage = $"Failed to parse URL from WEBSITE_RUN_FROM_PACKAGE value: {zipFilePath}";
                        result.FilesFound = false;
                        result.TargetFileFound = false;
                        return result;
                    }

                    // Get container URI by removing the blob name from the path
                    string uriWithoutBlob = zipFilePath.Substring(0, zipFilePath.LastIndexOf('/'));

                    // Check if there are any query parameters in the original URL and preserve them
                    string queryParams = string.Empty;
                    if (!string.IsNullOrEmpty(uri.Query))
                    {
                        queryParams = uri.Query;
                    }

                    // Add required container and list operation query parameters if not already present
                    bool hasRestype = queryParams.Contains("restype=container");
                    bool hasComp = queryParams.Contains("comp=list");

                    if (string.IsNullOrEmpty(queryParams))
                    {
                        queryParams = "?restype=container&comp=list";
                    }
                    else if (!hasRestype && !hasComp)
                    {
                        queryParams += "&restype=container&comp=list";
                    }
                    else if (!hasRestype)
                    {
                        queryParams += "&restype=container";
                    }
                    else if (!hasComp)
                    {
                        queryParams += "&comp=list";
                    }

                    containerPath = uriWithoutBlob + queryParams;

                    // Add extra logging for the constructed URI
                    _logger.LogInternalInformation("Constructed container URI for blob listing: {ContainerUri}", containerPath);
                }
                else
                {
                    // If containerPath was directly provided, try to determine the target file
                    // Check if it's a blob path or container path
                    Uri? uri;
                    if (Uri.TryCreate(containerPath, UriKind.Absolute, out uri))
                    {
                        string path = uri.AbsolutePath.TrimEnd('/');

                        // If it seems to be pointing to a specific file rather than a container
                        if (!path.EndsWith("/") && !containerPath.Contains("restype=container"))
                        {
                            targetFilePath = containerPath;
                            if (path.Contains('/'))
                            {
                                targetFileName = path.Substring(path.LastIndexOf('/') + 1);

                                // Convert file path to container path
                                containerPath = containerPath.Substring(0, containerPath.LastIndexOf('/'));

                                // Add required container and list operation query parameters if not already present
                                if (!containerPath.Contains("?"))
                                {
                                    containerPath += "?restype=container&comp=list";
                                }
                                else if (!containerPath.Contains("restype=container") && !containerPath.Contains("comp=list"))
                                {
                                    containerPath += "&restype=container&comp=list";
                                }
                                else if (!containerPath.Contains("restype=container"))
                                {
                                    containerPath += "&restype=container";
                                }
                                else if (!containerPath.Contains("comp=list"))
                                {
                                    containerPath += "&comp=list";
                                }
                            }
                        }
                    }

                    // Set the target file path in the result
                    result.TargetFilePath = targetFilePath;
                }

                // Store the container path we're verifying
                result.VerifiedContainerUri = containerPath;

                // Validate that the path is a proper URL
                if (!Uri.TryCreate(containerPath, UriKind.Absolute, out Uri? containerUri))
                {
                    result.IsSuccessful = false;
                    result.ErrorMessage = $"The provided container path is not a valid URL: {containerPath}";
                    result.FilesFound = false;
                    result.TargetFileFound = false;
                    return result;
                }

                // Call ListStorageBlobsAsync to get the list of blobs in the container
                _logger.LogInternalInformation("Calling ListStorageBlobsAsync for container: {ContainerPath}", containerPath);
                var blobListResult = await ListStorageBlobsAsync(containerPath);

                if (!blobListResult.IsSuccessful)
                {
                    result.IsSuccessful = false;
                    result.ErrorMessage = $"Failed to list blobs in container: {blobListResult.ErrorMessage}";
                    result.FilesFound = false;
                    result.TargetFileFound = false;
                    return result;
                }

                // Copy information from blobListResult to our result
                result.StorageAccountName = blobListResult.StorageAccountName;
                result.ContainerName = blobListResult.ContainerName;
                result.Files = blobListResult.Blobs;
                result.NextMarker = blobListResult.NextMarker;
                result.IsSuccessful = true;

                // Set FilesFound based on whether any files were found
                result.FilesFound = result.Files.Count > 0;

                // Check if the target file was found
                result.TargetFileFound = false;
                if (!string.IsNullOrEmpty(targetFileName) && result.FilesFound)
                {
                    // Look for the target file in the list of files
                    var targetFile = result.Files.FirstOrDefault(f => f.Name == targetFileName ||
                                                                    f.Name.EndsWith("/" + targetFileName));
                    result.TargetFileFound = targetFile != null;
                }

                // Add details about the files found
                int zipFileCount = result.Files.Count(f => f.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                                                         f.ContentType == "application/zip" ||
                                                         f.ContentType == "application/x-zip-compressed");

                long totalSize = result.Files.Sum(f => f.ContentLength);

                // Build the details message
                var detailsBuilder = new System.Text.StringBuilder();

                // First, add information about the target file if specified
                if (!string.IsNullOrEmpty(targetFilePath))
                {
                    if (result.TargetFileFound)
                    {
                        detailsBuilder.AppendLine($"TARGET FILE FOUND: The file '{targetFileName}' was found in the container '{result.ContainerName}'.");

                        // Add details about the found target file
                        var targetFile = result.Files.First(f => f.Name == targetFileName ||
                                                              f.Name.EndsWith("/" + targetFileName));
                        detailsBuilder.AppendLine($"  - Size: {FormatSize(targetFile.ContentLength)}");
                        detailsBuilder.AppendLine($"  - Last Modified: {targetFile.LastModified:yyyy-MM-dd HH:mm:ss}");
                        if (!string.IsNullOrEmpty(targetFile.ContentType))
                        {
                            detailsBuilder.AppendLine($"  - Content Type: {targetFile.ContentType}");
                        }
                    }
                    else
                    {
                        detailsBuilder.AppendLine($"TARGET FILE NOT FOUND: The file '{targetFileName}' was NOT found in the container '{result.ContainerName}'.");
                    }
                    detailsBuilder.AppendLine();
                }

                // Then add general information about all files in the container
                if (result.FilesFound)
                {
                    detailsBuilder.AppendLine($"CONTAINER CONTENTS: {result.Files.Count} files found in container '{result.ContainerName}'. " +
                                            $"Zip files: {zipFileCount}. Total size: {FormatSize(totalSize)}.");

                    // Get the most recently modified file
                    var newestFile = result.Files.OrderByDescending(f => f.LastModified).First();
                    detailsBuilder.AppendLine($"Most recent file: '{newestFile.Name}' (modified {newestFile.LastModified:yyyy-MM-dd HH:mm:ss}).");
                }
                else
                {
                    detailsBuilder.AppendLine($"EMPTY CONTAINER: The container '{result.ContainerName}' exists but contains no files.");
                }

                result.Details = detailsBuilder.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error verifying files in blob container for {ResourceId}", resourceId);
                result.IsSuccessful = false;
                result.ErrorMessage = $"An error occurred while verifying files in blob container: {ex.Message}";
                result.FilesFound = false;
                result.TargetFileFound = false;
            }

            return result;
        }

        /// <summary>
        /// Lists blobs in a storage container using ARM REST API
        /// </summary>
        /// <param name="containerUri">The URI of the container to list blobs from, including any query parameters</param>
        /// <returns>A result containing the list of blobs in the container</returns>
        private async Task<StorageBlobListResult> ListStorageBlobsAsync(string containerUri)
        {
            var result = new StorageBlobListResult();
            var allBlobs = new List<StorageBlobItem>();
            bool hasMoreResults = true;
            string nextMarker = string.Empty;

            try
            {
                _logger.LogInternalInformation("Listing blobs from container: {ContainerUri}", containerUri);

                if (string.IsNullOrWhiteSpace(containerUri))
                {
                    result.IsSuccessful = false;
                    result.ErrorMessage = "Container URI cannot be empty";
                    return result;
                }

                if (!Uri.TryCreate(containerUri, UriKind.Absolute, out Uri? uri))
                {
                    result.IsSuccessful = false;
                    result.ErrorMessage = $"Invalid container URI: {containerUri}";
                    return result;
                }

                // Check for required query parameters
                bool hasCompList = uri.Query.Contains("comp=list");
                bool hasRestype = uri.Query.Contains("restype=container");
                bool hasPrefix = uri.Query.Contains("prefix=");
                bool hasDelimiter = uri.Query.Contains("delimiter=");
                bool hasMaxResults = uri.Query.Contains("maxresults=");

                // Store container URI for the result
                result.ContainerUri = containerUri;

                // If URI is missing required parameters, add them
                if (!hasCompList || !hasRestype || !hasPrefix || !hasDelimiter || !hasMaxResults)
                {
                    string updatedUri = containerUri;

                    // Ensure we have the required comp=list parameter
                    if (!hasCompList)
                        updatedUri = AddQueryParam(updatedUri, "comp", "list");

                    // Ensure we have the required restype=container parameter
                    if (!hasRestype)
                        updatedUri = AddQueryParam(updatedUri, "restype", "container");

                    // Add optional parameters if missing
                    if (!hasPrefix)
                        updatedUri = AddQueryParam(updatedUri, "prefix", "");

                    if (!hasDelimiter)
                        updatedUri = AddQueryParam(updatedUri, "delimiter", "/");

                    if (!hasMaxResults)
                        updatedUri = AddQueryParam(updatedUri, "maxresults", "5000");

                    // Update the container URI with the modified one
                    containerUri = updatedUri;
                    _logger.LogInternalInformation("Updated container URI with required parameters: {ContainerUri}", containerUri);
                }

                // Get a token for storage access
                var cred = await _authService.GetArmOperationCredential();

                while (hasMoreResults)
                {
                    // Prepare the request URI for the current page
                    string pageRequestUri = containerUri;

                    // If we have a next marker from a previous page, add it to the request
                    if (!string.IsNullOrEmpty(nextMarker))
                    {
                        // Add the marker parameter to the URI
                        if (pageRequestUri.Contains('?'))
                            pageRequestUri += $"&marker={Uri.EscapeDataString(nextMarker)}";
                        else
                            pageRequestUri += $"?marker={Uri.EscapeDataString(nextMarker)}";

                        _logger.LogInternalInformation("Added next marker to request: {NextMarker}", nextMarker);
                    }

                    // Create an HTTP client
                    var httpClient = _httpClientFactory.CreateClient();

                    // Get a token for storage access
                    var tokenRequestContext = new TokenRequestContext(new[] { "https://storage.azure.com/.default" });
                    var token = await cred.GetTokenAsync(tokenRequestContext, CancellationToken.None);

                    // Add token to authorization header
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

                    // Make the request
                    var response = await httpClient.GetAsync(pageRequestUri);

                    if (!response.IsSuccessStatusCode)
                    {
                        result.IsSuccessful = false;
                        result.ErrorMessage = $"Failed to list blobs. Status code: {response.StatusCode}, Reason: {response.ReasonPhrase}";

                        // Try to get the response content for additional error details
                        try
                        {
                            string errorContent = await response.Content.ReadAsStringAsync();
                            if (!string.IsNullOrWhiteSpace(errorContent))
                            {
                                _logger.LogInternalWarning("Error response from storage API: {ErrorContent}",
                                    errorContent.Length <= 1000 ? errorContent : errorContent.Substring(0, 1000) + "...");
                                result.ErrorMessage += $". Details: {errorContent}";
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInternalWarning("Failed to read error content: {ErrorMessage}", ex.Message);
                        }

                        return result;
                    }

                    // Get the XML response
                    string xmlResponse = await response.Content.ReadAsStringAsync();

                    // Log the XML response for debugging
                    _logger.LogInternalInformation("Received response of length {Length} from storage", xmlResponse.Length);
                    if (xmlResponse.Length > 0 && xmlResponse.Length <= 1000)
                    {
                        _logger.LogInternalInformation("Response content: {Content}", xmlResponse);
                    }
                    else if (xmlResponse.Length > 1000)
                    {
                        _logger.LogInternalInformation("Response content (truncated): {Content}", xmlResponse.Substring(0, 1000) + "...");
                    }

                    // Check if XML response is empty or whitespace
                    if (string.IsNullOrWhiteSpace(xmlResponse))
                    {
                        result.IsSuccessful = false;
                        result.ErrorMessage = "Empty response received from storage API";
                        return result;
                    }

                    // If the response doesn't look like XML, try to handle it as a special case
                    if (!xmlResponse.TrimStart().StartsWith("<"))
                    {
                        _logger.LogInternalWarning("Response doesn't appear to be XML: {FirstChars}",
                            xmlResponse.Length <= 50 ? xmlResponse : xmlResponse.Substring(0, 50) + "...");

                        // If empty container, create a valid result with no blobs
                        if (xmlResponse.Contains("The specified container is empty") ||
                            xmlResponse.Contains("The specified blob does not exist"))
                        {
                            result.IsSuccessful = true;
                            result.Details = "The container exists but is empty.";
                            return result;
                        }

                        result.IsSuccessful = false;
                        result.ErrorMessage = "Invalid response format from storage API";
                        return result;
                    }

                    // Parse the XML response with proper error handling
                    XmlDocument xmlDoc = new XmlDocument();
                    try
                    {
                        // Load the XML with appropriate settings
                        var settings = new XmlReaderSettings
                        {
                            DtdProcessing = DtdProcessing.Prohibit,
                            MaxCharactersFromEntities = 1024,
                            XmlResolver = null
                        };

                        using var stringReader = new System.IO.StringReader(xmlResponse);
                        using var xmlReader = XmlReader.Create(stringReader, settings);
                        xmlDoc.Load(xmlReader);

                        if (xmlDoc.DocumentElement == null)
                        {
                            result.IsSuccessful = false;
                            result.ErrorMessage = "XML response has no document element";
                            return result;
                        }
                    }
                    catch (XmlException ex)
                    {
                        _logger.LogInternalError(ex, "XML parsing error: {ErrorMessage}. XML Content: {XmlContent}",
                            ex.Message,
                            xmlResponse.Length <= 1000 ? xmlResponse : xmlResponse.Substring(0, 1000) + "...");

                        // Try alternative parsing approach
                        try
                        {
                            xmlDoc.LoadXml(xmlResponse);
                        }
                        catch (Exception altEx)
                        {
                            _logger.LogInternalError(altEx, "Alternative XML parsing also failed: {ErrorMessage}", altEx.Message);
                            result.IsSuccessful = false;
                            result.ErrorMessage = $"Failed to parse XML response: {ex.Message}";
                            return result;
                        }
                    }

                    // Parse the container and blobs information
                    var nsManager = new XmlNamespaceManager(xmlDoc.NameTable);
                    nsManager.AddNamespace("d", "http://schemas.microsoft.com/ado/2007/08/dataservices");
                    nsManager.AddNamespace("m", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata");

                    // Check if we have a valid EnumerationResults element
                    var enumerationResults = xmlDoc.DocumentElement;
                    if (enumerationResults?.Name != "EnumerationResults")
                    {
                        result.IsSuccessful = false;
                        result.ErrorMessage = $"Invalid XML response format: Expected 'EnumerationResults' root element but found '{enumerationResults?.Name ?? "null"}'";
                        return result;
                    }

                    // Extract ServiceEndpoint and ContainerName attributes if available
                    if (enumerationResults.HasAttribute("ServiceEndpoint"))
                    {
                        var serviceEndpoint = enumerationResults.GetAttribute("ServiceEndpoint");
                        if (!string.IsNullOrEmpty(serviceEndpoint))
                        {
                            try
                            {
                                Uri serviceEndpointUri = new Uri(serviceEndpoint);
                                result.StorageAccountName = serviceEndpointUri.Host.Split('.')[0];
                            }
                            catch (Exception ex)
                            {
                                _logger.LogInternalWarning("Failed to parse ServiceEndpoint URI '{ServiceEndpoint}': {ErrorMessage}",
                                    serviceEndpoint, ex.Message);
                                // Continue processing even if we can't parse the service endpoint
                            }
                        }
                    }

                    if (enumerationResults.HasAttribute("ContainerName"))
                    {
                        result.ContainerName = enumerationResults.GetAttribute("ContainerName");
                    }

                    // Process the current page of results
                    var pageBlobs = new List<StorageBlobItem>();
                    try
                    {
                        // Check for NextMarker for pagination
                        var nextMarkerNode = xmlDoc.SelectSingleNode("//NextMarker");
                        if (nextMarkerNode != null && !string.IsNullOrWhiteSpace(nextMarkerNode.InnerText))
                        {
                            nextMarker = nextMarkerNode.InnerText;
                            result.NextMarker = nextMarker;
                            hasMoreResults = true;
                            _logger.LogInternalInformation("Found NextMarker: {NextMarker}", nextMarker);
                        }
                        else
                        {
                            nextMarker = string.Empty;
                            hasMoreResults = false;
                            _logger.LogInternalInformation("No NextMarker found, this is the last page of results");
                        }

                        // Parse the blobs
                        var blobsNode = xmlDoc.SelectSingleNode("//Blobs");
                        if (blobsNode != null)
                        {
                            var blobNodes = xmlDoc.SelectNodes("//Blob");
                            if (blobNodes != null)
                            {
                                foreach (XmlNode blobNode in blobNodes)
                                {
                                    var blobItem = new StorageBlobItem();

                                    // Get the blob name
                                    var nameNode = blobNode.SelectSingleNode("Name");
                                    if (nameNode != null)
                                    {
                                        blobItem.Name = nameNode.InnerText;
                                    }

                                    // Parse the properties
                                    var propertiesNode = blobNode.SelectSingleNode("Properties");
                                    if (propertiesNode != null)
                                    {
                                        // Extract common properties
                                        var contentLengthNode = propertiesNode.SelectSingleNode("Content-Length");
                                        if (contentLengthNode != null && long.TryParse(contentLengthNode.InnerText, out long contentLength))
                                        {
                                            blobItem.ContentLength = contentLength;
                                        }

                                        var contentTypeNode = propertiesNode.SelectSingleNode("Content-Type");
                                        if (contentTypeNode != null)
                                        {
                                            blobItem.ContentType = contentTypeNode.InnerText;
                                        }

                                        var etagNode = propertiesNode.SelectSingleNode("Etag");
                                        if (etagNode != null)
                                        {
                                            blobItem.ETag = etagNode.InnerText;
                                        }

                                        var contentMD5Node = propertiesNode.SelectSingleNode("Content-MD5");
                                        if (contentMD5Node != null)
                                        {
                                            blobItem.ContentMD5 = contentMD5Node.InnerText;
                                        }

                                        var blobTypeNode = propertiesNode.SelectSingleNode("BlobType");
                                        if (blobTypeNode != null)
                                        {
                                            blobItem.BlobType = blobTypeNode.InnerText;
                                        }

                                        var leaseStatusNode = propertiesNode.SelectSingleNode("LeaseStatus");
                                        if (leaseStatusNode != null)
                                        {
                                            blobItem.LeaseStatus = leaseStatusNode.InnerText;
                                        }

                                        var creationTimeNode = propertiesNode.SelectSingleNode("Creation-Time");
                                        if (creationTimeNode != null && DateTime.TryParse(creationTimeNode.InnerText, out DateTime creationTime))
                                        {
                                            blobItem.CreationTime = creationTime;
                                        }

                                        var lastModifiedNode = propertiesNode.SelectSingleNode("Last-Modified");
                                        if (lastModifiedNode != null && DateTime.TryParse(lastModifiedNode.InnerText, out DateTime lastModified))
                                        {
                                            blobItem.LastModified = lastModified;
                                        }
                                    }

                                    // Add the blob to the current page results
                                    pageBlobs.Add(blobItem);
                                }
                            }
                        }

                        // Add the current page blobs to the overall results
                        allBlobs.AddRange(pageBlobs);
                        _logger.LogInternalInformation("Added {Count} blobs from current page, total count now: {TotalCount}",
                            pageBlobs.Count, allBlobs.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalError(ex, "Error parsing blob information from XML: {ErrorMessage}", ex.Message);
                        // Continue with whatever blobs we've already parsed
                        // We'll still try to process more pages if available
                    }

                    // If we didn't find any blobs on this page, no need to continue
                    if (pageBlobs.Count == 0)
                    {
                        _logger.LogInternalInformation("No blobs found on current page, stopping pagination");
                        hasMoreResults = false;
                    }
                }

                // Set the final results
                result.Blobs = allBlobs;
                result.IsSuccessful = true;

                // If we have no blobs but the operation is otherwise successful, add a note
                if (result.Blobs.Count == 0)
                {
                    _logger.LogInternalInformation("No blobs found in container {ContainerName}", result.ContainerName);
                }
                else
                {
                    _logger.LogInternalInformation("Successfully retrieved {Count} blobs from container {ContainerName}",
                        result.Blobs.Count, result.ContainerName);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error listing blobs from container: {ContainerUri}", containerUri);
                result.IsSuccessful = false;
                result.ErrorMessage = $"An error occurred while listing blobs: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Gets the package size from blob properties when the stream doesn't support Length
        /// </summary>
        /// <param name="packageUrl">The URL of the package</param>
        /// <returns>The size of the package in bytes, or 0 if size could not be determined</returns>
        private async Task<long> GetPackageSizeFromPropertiesAsync(string packageUrl)
        {
            try
            {
                if (!Uri.TryCreate(packageUrl, UriKind.Absolute, out Uri? packageUri))
                {
                    _logger.LogInternalWarning("Invalid package URL for size check: {PackageUrl}", packageUrl);
                    return 0;
                }

                // Check if it's an Azure blob URL
                if (TryParseBlobUri(packageUri, out string accountName, out string containerName, out string blobPath))
                {
                    try
                    {
                        var properties = await _blobStorageClient.GetBlobPropertiesAsync(containerName, blobPath, CancellationToken.None);
                        return properties.ContentLength;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalWarning("Failed to get blob properties for size check: {Error}", ex.Message);
                        
                        // Fallback to HTTP HEAD request
                        return await GetPackageSizeViaHttpAsync(packageUrl);
                    }
                }

                // For non-Azure URLs, use HTTP HEAD request
                return await GetPackageSizeViaHttpAsync(packageUrl);
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning("Error getting package size from properties for {PackageUrl}: {Error}", packageUrl, ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Gets the package size using HTTP HEAD request
        /// </summary>
        /// <param name="packageUrl">The URL of the package</param>
        /// <returns>The size of the package in bytes, or 0 if size could not be determined</returns>
        private async Task<long> GetPackageSizeViaHttpAsync(string packageUrl)
        {
            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                using var request = new HttpRequestMessage(HttpMethod.Head, packageUrl);
                using var response = await httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode && response.Content.Headers.ContentLength.HasValue)
                {
                    return response.Content.Headers.ContentLength.Value;
                }

                _logger.LogInternalWarning("Unable to determine package size via HTTP HEAD for {PackageUrl}: {StatusCode}", 
                    packageUrl, response.StatusCode);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning("Error getting package size via HTTP for {PackageUrl}: {Error}", packageUrl, ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Downloads a package stream from the specified URL with security measures and retry logic
        /// </summary>
        /// <param name="packageUrl">The URL of the package to download</param>
        /// <returns>Stream containing the package data, or null if download failed</returns>
        private async Task<Stream?> GetPackageStreamAsync(string packageUrl)
        {
            const long MaxPackageSize = 500 * 1024 * 1024; // 500 MB limit
            const int TimeoutMinutes = 5;
            const int MaxRetries = 3;
            const int BaseDelayMs = 1000;

            try
            {
                if (!Uri.TryCreate(packageUrl, UriKind.Absolute, out Uri? packageUri))
                {
                    _logger.LogInternalError("Invalid package URL: {PackageUrl}", packageUrl);
                    return null;
                }

                // Check if it's an Azure blob URL and use blob storage client with retry logic
                if (TryParseBlobUri(packageUri, out string accountName, out string containerName, out string blobPath))
                {
                    _logger.LogInternalInformation("Using Azure Blob Storage client to download package from {AccountName}/{ContainerName}", accountName, containerName);
                    
                    for (int attempt = 1; attempt <= MaxRetries; attempt++)
                    {
                        try
                        {
                            return await _blobStorageClient.DownloadBlobContentsAsStreamAsync(packageUri);
                        }
                        catch (Exception ex) when (attempt < MaxRetries && IsRetryableException(ex))
                        {
                            var delay = BaseDelayMs * (int)Math.Pow(2, attempt - 1); // Exponential backoff
                            _logger.LogInternalWarning("Blob download attempt {Attempt} failed, retrying in {Delay}ms: {Error}", 
                                attempt, delay, ex.Message);
                            await Task.Delay(delay);
                        }
                    }
                    
                    // Final attempt without retry - let the exception bubble up
                    return await _blobStorageClient.DownloadBlobContentsAsStreamAsync(packageUri);
                }

                // Fallback to HTTP download for other URLs with retry logic
                for (int attempt = 1; attempt <= MaxRetries; attempt++)
                {
                    try
                    {
                        using var httpClient = _httpClientFactory.CreateClient();
                        httpClient.Timeout = TimeSpan.FromMinutes(TimeoutMinutes);

                        var response = await httpClient.GetAsync(packageUrl, HttpCompletionOption.ResponseHeadersRead);
                        response.EnsureSuccessStatusCode();

                        // Check content length before downloading
                        if (response.Content.Headers.ContentLength.HasValue)
                        {
                            if (response.Content.Headers.ContentLength.Value > MaxPackageSize)
                            {
                                _logger.LogInternalError("Package size ({Size} bytes) exceeds maximum allowed size", response.Content.Headers.ContentLength.Value);
                                return null;
                            }
                        }

                        // Download to memory stream with size monitoring
                        var memoryStream = new MemoryStream();
                        using var contentStream = await response.Content.ReadAsStreamAsync();
                        
                        var buffer = new byte[8192];
                        int bytesRead;
                        long totalBytesRead = 0;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            totalBytesRead += bytesRead;
                            if (totalBytesRead > MaxPackageSize)
                            {
                                _logger.LogInternalError("Package download exceeded maximum size during transfer");
                                memoryStream.Dispose();
                                return null;
                            }

                            memoryStream.Write(buffer, 0, bytesRead);
                        }

                        memoryStream.Position = 0;
                        return memoryStream;
                    }
                    catch (Exception ex) when (attempt < MaxRetries && IsRetryableException(ex))
                    {
                        var delay = BaseDelayMs * (int)Math.Pow(2, attempt - 1); // Exponential backoff
                        _logger.LogInternalWarning("HTTP download attempt {Attempt} failed, retrying in {Delay}ms: {Error}", 
                            attempt, delay, ex.Message);
                        await Task.Delay(delay);
                    }
                }
                
                // Final attempt without retry - let the exception bubble up for proper logging
                using var finalHttpClient = _httpClientFactory.CreateClient();
                finalHttpClient.Timeout = TimeSpan.FromMinutes(TimeoutMinutes);

                var finalResponse = await finalHttpClient.GetAsync(packageUrl, HttpCompletionOption.ResponseHeadersRead);
                finalResponse.EnsureSuccessStatusCode();

                using var finalContentStream = await finalResponse.Content.ReadAsStreamAsync();
                var finalMemoryStream = new MemoryStream();
                await finalContentStream.CopyToAsync(finalMemoryStream);
                finalMemoryStream.Position = 0;
                return finalMemoryStream;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error downloading package from {PackageUrl} after {MaxRetries} attempts", packageUrl, MaxRetries);
                return null;
            }
        }

        /// <summary>
        /// Determines if an exception is retryable for network operations
        /// </summary>
        /// <param name="ex">The exception to check</param>
        /// <returns>True if the exception indicates a transient failure that can be retried</returns>
        private static bool IsRetryableException(Exception ex)
        {
            return ex switch
            {
                HttpRequestException httpEx => 
                    httpEx.Message.Contains("connection was aborted") ||
                    httpEx.Message.Contains("timeout") ||
                    httpEx.Message.Contains("network") ||
                    httpEx.Message.Contains("host machine") ||
                    httpEx.InnerException is SocketException,
                TaskCanceledException => true, // Usually timeout
                SocketException => true,
                IOException ioEx => 
                    ioEx.Message.Contains("connection was aborted") ||
                    ioEx.Message.Contains("Unable to read data"),
                _ => false
            };
        }

        /// <summary>
        /// Tests basic network connectivity to a host
        /// </summary>
        /// <param name="uri">The URI to test connectivity to</param>
        /// <returns>True if basic connectivity is available</returns>
        private async Task<bool> TestNetworkConnectivityAsync(Uri uri)
        {
            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10); // Short timeout for connectivity test
                
                // Use HEAD request to minimize data transfer
                using var request = new HttpRequestMessage(HttpMethod.Head, uri);
                using var response = await httpClient.SendAsync(request);
                
                // Any response (even error codes) indicates network connectivity
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning("Network connectivity test failed for {Host}: {Error}", uri.Host, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Inspects the ZIP structure and populates the report
        /// </summary>
        /// <param name="zipStream">Stream containing the ZIP data</param>
        /// <param name="report">Report to populate with inspection results</param>
        private async Task InspectZipStructureAsync(Stream zipStream, PackageStructureReport report)
        {
            try
            {
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
                
                var fileNames = new List<string>();
                var rootFiles = new List<string>();
                var functionFolders = new List<string>();

                foreach (var entry in archive.Entries)
                {
                    // Skip directories (entries with names ending in '/')
                    if (entry.FullName.EndsWith('/'))
                        continue;

                    fileNames.Add(entry.FullName);

                    // Check for root level files
                    if (!entry.FullName.Contains('/'))
                    {
                        rootFiles.Add(entry.FullName);
                    }
                    else
                    {
                        // Check for function folders (folders containing function.json)
                        var folderName = entry.FullName.Split('/')[0];
                        if (entry.FullName.EndsWith("function.json", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!functionFolders.Contains(folderName))
                                functionFolders.Add(folderName);
                        }
                    }
                }

                report.FileCount = fileNames.Count;
                report.Functions = functionFolders;
                report.FunctionCount = functionFolders.Count;

                // Analyze file types
                AnalyzeFileTypes(fileNames, report);

                // Detect runtime
                report.DetectedRuntime = DetectFunctionAppRuntime(fileNames);

                // Validate structure based on runtime
                ValidateStructure(fileNames, rootFiles, functionFolders, report);

                // Generate root structure description
                report.RootStructure = GenerateRootStructureDescription(rootFiles, functionFolders);

                // Generate recommendations
                GenerateRecommendations(report);

                await Task.CompletedTask; // Make method async-compatible
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error inspecting ZIP structure");
                report.StructureIssues.Add($"Failed to analyze ZIP structure: {ex.Message}");
                report.HasValidStructure = false;
            }
        }

        /// <summary>
        /// Detects the Function App runtime based on file patterns
        /// </summary>
        /// <param name="fileNames">List of all file names in the package</param>
        /// <returns>Detected runtime name</returns>
        private string DetectFunctionAppRuntime(List<string> fileNames)
        {
            var lowerFileNames = fileNames.Select(f => f.ToLowerInvariant()).ToList();

            // .NET runtime detection - prioritize isolated detection
            if (lowerFileNames.Any(f => f.EndsWith(".dll") && !f.Contains("/"))) // Root level DLL
            {
                bool hasDepsJson = lowerFileNames.Any(f => f.EndsWith(".deps.json"));
                bool hasRuntimeConfig = lowerFileNames.Any(f => f.EndsWith(".runtimeconfig.json"));
                bool hasAzureFunctionsDll = lowerFileNames.Any(f => f.Contains("microsoft.azure.functions") && f.EndsWith(".dll"));
                bool hasAzureFunctionsWorkerDll = lowerFileNames.Any(f => f.Contains("microsoft.azure.functions.worker") && f.EndsWith(".dll"));

                if (hasDepsJson || hasRuntimeConfig)
                {
                    // Check for .NET isolated Functions specific indicators
                    if (hasAzureFunctionsWorkerDll)
                    {
                        return ".NET Isolated";
                    }
                    else if (hasAzureFunctionsDll)
                    {
                        return ".NET In-Process";
                    }
                    else
                    {
                        return ".NET";
                    }
                }
            }

            // Node.js runtime detection
            if (lowerFileNames.Contains("package.json"))
                return "Node.js";

            // Python runtime detection
            if (lowerFileNames.Contains("requirements.txt"))
                return "Python";

            // Java runtime detection  
            if (lowerFileNames.Any(f => f.EndsWith("pom.xml")) || lowerFileNames.Any(f => f.EndsWith(".jar")))
                return "Java";

            // PowerShell runtime detection
            if (lowerFileNames.Any(f => f.EndsWith(".psm1") || f.EndsWith(".ps1")))
                return "PowerShell";

            return "Unknown";
        }

        /// <summary>
        /// Gets required files for the specified runtime
        /// </summary>
        /// <param name="runtime">The detected runtime</param>
        /// <returns>List of required files</returns>
        private List<string> GetRequiredFilesForRuntime(string runtime)
        {
            var required = new List<string> { "host.json" }; // All runtimes need host.json

            switch (runtime)
            {
                case ".NET":
                    // .NET requires at least one DLL and typically deps.json
                    break;
                case "Node.js":
                    required.Add("package.json");
                    break;
                case "Python":
                    required.Add("requirements.txt");
                    break;
                case "Java":
                    // Java may have pom.xml or JAR files
                    break;
                case "PowerShell":
                    // PowerShell modules may have various structures
                    break;
            }

            return required;
        }

        /// <summary>
        /// Validates the package structure based on runtime requirements
        /// </summary>
        /// <param name="fileNames">All file names in package</param>
        /// <param name="rootFiles">Root level file names</param>
        /// <param name="functionFolders">Function folder names</param>
        /// <param name="report">Report to update with validation results</param>
        private void ValidateStructure(List<string> fileNames, List<string> rootFiles, List<string> functionFolders, PackageStructureReport report)
        {
            var lowerRootFiles = rootFiles.Select(f => f.ToLowerInvariant()).ToList();
            var lowerFileNames = fileNames.Select(f => f.ToLowerInvariant()).ToList();
            
            // Check for host.json
            report.HasHostJson = lowerRootFiles.Contains("host.json");
            if (!report.HasHostJson)
            {
                report.MissingRequiredFiles.Add("host.json");
                report.StructureIssues.Add("Missing required host.json file at root level");
            }

            // Check if functions have function.json files
            var functionsWithoutJson = new List<string>();
            foreach (var folder in functionFolders)
            {
                var functionJsonPath = $"{folder}/function.json";
                if (!fileNames.Any(f => f.Equals(functionJsonPath, StringComparison.OrdinalIgnoreCase)))
                {
                    functionsWithoutJson.Add(folder);
                }
            }

            if (functionsWithoutJson.Any())
            {
                report.StructureIssues.Add($"Functions missing function.json: {string.Join(", ", functionsWithoutJson)}");
            }

            // Runtime-specific validation
            var requiredFiles = GetRequiredFilesForRuntime(report.DetectedRuntime);
            foreach (var requiredFile in requiredFiles)
            {
                if (!lowerRootFiles.Contains(requiredFile.ToLowerInvariant()))
                {
                    report.MissingRequiredFiles.Add(requiredFile);
                }
            }

            // Check for .NET isolated Functions pattern
            bool isDotNetIsolated = (report.DetectedRuntime == ".NET Isolated" || 
                                   (report.DetectedRuntime.StartsWith(".NET") && 
                                    lowerRootFiles.Any(f => f.EndsWith(".dll")) &&
                                    (lowerRootFiles.Any(f => f.EndsWith(".deps.json")) || 
                                     lowerRootFiles.Any(f => f.EndsWith(".runtimeconfig.json")))));

            // Check for common issues - handle .NET isolated scenario
            if (functionFolders.Count == 0)
            {
                if (isDotNetIsolated)
                {
                    // For .NET isolated Functions, function folders are not required
                    // Functions are discovered via reflection from compiled DLLs
                    report.StructureIssues.Add("No function folders detected. This is expected for .NET isolated Azure Functions where functions are compiled into DLLs and discovered using reflection.");
                }
                else
                {
                    report.StructureIssues.Add("No function folders detected - package may be incorrectly structured");
                }
            }

            // Determine overall validity - handle .NET isolated scenario
            report.HasRequiredFiles = report.MissingRequiredFiles.Count == 0;
            
            if (isDotNetIsolated)
            {
                // For .NET isolated Functions, we don't require function folders or function.json files
                // The structure is valid if host.json is present and there are DLLs
                report.HasValidStructure = report.HasHostJson && 
                                         report.HasRequiredFiles &&
                                         lowerRootFiles.Any(f => f.EndsWith(".dll"));
                
                // Remove the "No function folders detected" issue for .NET isolated
                report.StructureIssues.RemoveAll(issue => 
                    issue.Contains("No function folders detected") && 
                    issue.Contains("This is expected for .NET isolated Azure Functions"));
            }
            else
            {
                // For traditional Functions, require function folders and function.json files
                report.HasValidStructure = report.HasHostJson && 
                                         functionFolders.Count > 0 && 
                                         functionsWithoutJson.Count == 0 &&
                                         report.StructureIssues.Count == 0;
            }

            report.FolderStructureValid = report.HasValidStructure;
        }

        /// <summary>
        /// Analyzes file types and updates the report
        /// </summary>
        /// <param name="fileNames">List of all file names</param>
        /// <param name="report">Report to update</param>
        private void AnalyzeFileTypes(List<string> fileNames, PackageStructureReport report)
        {
            foreach (var fileName in fileNames)
            {
                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                if (string.IsNullOrEmpty(extension))
                    extension = "no-extension";

                if (report.FileTypeCounts.ContainsKey(extension))
                    report.FileTypeCounts[extension]++;
                else
                    report.FileTypeCounts[extension] = 1;
            }
        }

        /// <summary>
        /// Generates a description of the root structure
        /// </summary>
        /// <param name="rootFiles">Root level files</param>
        /// <param name="functionFolders">Function folder names</param>
        /// <returns>Description string</returns>
        private string GenerateRootStructureDescription(List<string> rootFiles, List<string> functionFolders)
        {
            var description = new StringBuilder();
            description.AppendLine("Root level contents:");
            
            if (rootFiles.Any())
            {
                description.AppendLine($"  Files: {string.Join(", ", rootFiles.Take(10))}");
                if (rootFiles.Count > 10)
                    description.AppendLine($"  ... and {rootFiles.Count - 10} more files");
            }

            if (functionFolders.Any())
            {
                description.AppendLine($"  Function folders: {string.Join(", ", functionFolders)}");
            }

            return description.ToString().TrimEnd();
        }

        /// <summary>
        /// Generates recommendations for the package
        /// </summary>
        /// <param name="report">Report to update with recommendations</param>
        private void GenerateRecommendations(PackageStructureReport report)
        {
            if (!report.HasHostJson)
            {
                report.Recommendations.Add("Add host.json file at the root level to configure the Function App runtime");
            }

            // Check if this is a .NET isolated Functions scenario
            bool isDotNetIsolated = (report.DetectedRuntime == ".NET Isolated" || 
                                   (report.DetectedRuntime.StartsWith(".NET") && 
                                    report.FileTypeCounts.ContainsKey(".dll") &&
                                    (report.FileTypeCounts.ContainsKey(".deps.json") || 
                                     report.FileTypeCounts.ContainsKey(".runtimeconfig.json"))));

            if (report.FunctionCount == 0)
            {
                if (isDotNetIsolated)
                {
                    report.Recommendations.Add("For .NET isolated Azure Functions, functions are compiled into DLLs and discovered using reflection. Ensure your compiled DLLs contain function definitions with proper Azure Functions attributes.");
                }
                else
                {
                    report.Recommendations.Add("Ensure the package contains function folders with function.json files");
                }
            }

            if (report.DetectedRuntime == "Unknown")
            {
                report.Recommendations.Add("Package runtime could not be determined - ensure proper runtime files are included");
            }

            if (report.FileCount > 10000)
            {
                report.Recommendations.Add("Package contains a large number of files - consider optimizing dependencies");
            }

            if (report.PackageSize > 100 * 1024 * 1024) // 100 MB
            {
                report.Recommendations.Add("Package size is large - consider reducing dependencies or using external storage");
            }
        }

        /// <summary>
        /// Checks if package size is within acceptable limits
        /// </summary>
        /// <param name="sizeBytes">Size in bytes</param>
        /// <returns>True if size is acceptable</returns>
        private bool IsPackageSizeAcceptable(long sizeBytes)
        {
            const long MaxPackageSize = 500 * 1024 * 1024; // 500 MB
            return sizeBytes <= MaxPackageSize;
        }

        /// <summary>
        /// Gets package metadata using HTTP HEAD request
        /// </summary>
        /// <param name="packageUrl">URL of the package</param>
        /// <param name="metadata">Metadata object to populate</param>
        private async Task GetMetadataViaHttpAsync(string packageUrl, PackageMetadata metadata)
        {
            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromMinutes(1);

                using var request = new HttpRequestMessage(HttpMethod.Head, packageUrl);
                using var response = await httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    metadata.Size = response.Content.Headers.ContentLength ?? 0;
                    metadata.LastModified = response.Content.Headers.LastModified?.DateTime;
                    metadata.ContentType = response.Content.Headers.ContentType?.ToString() ?? string.Empty;
                    metadata.ETag = response.Headers.ETag?.ToString() ?? string.Empty;
                    metadata.IsSuccessful = true;
                }
                else
                {
                    metadata.IsSuccessful = false;
                    metadata.ErrorMessage = $"HTTP HEAD request failed: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error getting metadata via HTTP for {PackageUrl}", packageUrl);
                metadata.IsSuccessful = false;
                metadata.ErrorMessage = $"Failed to retrieve metadata via HTTP: {ex.Message}";
            }
        }

        #region Security Implementation

        /// <summary>
        /// Sanitizes a URL by removing SAS tokens and other sensitive parameters
        /// </summary>
        /// <param name="url">The URL to sanitize</param>
        /// <returns>The sanitized URL with sensitive parameters removed</returns>
        private string SanitizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            try
            {
                var uri = new Uri(url);
                var baseUrl = $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";

                // Parse query parameters and remove sensitive ones
                var queryParams = HttpUtility.ParseQueryString(uri.Query);
                var sensitiveParams = new[] { "sig", "st", "se", "sp", "sv", "sr", "spr", "sip", "key", "token", "password", "pwd" };

                // Remove sensitive parameters
                foreach (var param in sensitiveParams)
                {
                    queryParams.Remove(param);
                }

                // Rebuild URL with remaining parameters
                if (queryParams.Count > 0)
                {
                    return $"{baseUrl}?{queryParams}";
                }

                return baseUrl;
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning("Failed to sanitize URL: {Error}", ex.Message);
                // Return a generic placeholder if parsing fails
                return "[SANITIZED_URL]";
            }
        }

        /// <summary>
        /// Redacts storage keys and connection strings by replacing with asterisks
        /// </summary>
        /// <param name="value">The value that may contain storage keys</param>
        /// <returns>The value with storage keys redacted</returns>
        private string RedactStorageKeys(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            try
            {
                // Pattern for Azure Storage Account Keys (base64 encoded, typically 88 characters but can vary)
                var storageKeyPattern = @"^[A-Za-z0-9+/]{60,}={0,2}$";
                
                // Pattern for connection strings with AccountKey
                var connectionStringPattern = @"AccountKey=[^;]+";
                
                // Pattern for SAS tokens
                var sasTokenPattern = @"sig=[^&\s]+";

                // If the entire value matches a storage key pattern, redact completely
                if (System.Text.RegularExpressions.Regex.IsMatch(value, storageKeyPattern))
                {
                    return "****";
                }
                
                // Replace connection string keys
                value = System.Text.RegularExpressions.Regex.Replace(value, connectionStringPattern, "AccountKey=****");
                
                // Replace SAS signatures
                value = System.Text.RegularExpressions.Regex.Replace(value, sasTokenPattern, "sig=****");

                return value;
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning("Failed to redact storage keys: {Error}", ex.Message);
                return "[REDACTED]";
            }
        }

        /// <summary>
        /// Validates package accessibility without exposing content or sensitive information
        /// </summary>
        /// <param name="resourceId">The Azure resource ID</param>
        /// <param name="packageUrl">The package URL to validate</param>
        /// <returns>Validation result without exposing sensitive data</returns>
        private async Task<PackageAccessibilityResult> ValidateWithoutExposing(string resourceId, string packageUrl)
        {
            var result = new PackageAccessibilityResult
            {
                ResourceId = resourceId,
                PackageUrl = SanitizeUrl(packageUrl)
            };

            try
            {
                _logger.LogInternalInformation("Validating package accessibility for {ResourceId}", resourceId);

                if (string.IsNullOrWhiteSpace(packageUrl))
                {
                    result.IsAccessible = false;
                    result.ErrorDetails = "Package URL is empty";
                    result.IsSuccessful = true;
                    result.Recommendations.Add("Provide a valid package URL");
                    return result;
                }

                // Try to parse the URL
                if (!Uri.TryCreate(packageUrl, UriKind.Absolute, out Uri? uri))
                {
                    result.IsAccessible = false;
                    result.ErrorDetails = "Invalid URL format";
                    result.IsSuccessful = true;
                    result.Recommendations.Add("Ensure the URL is properly formatted");
                    return result;
                }

                // Determine storage type
                if (uri.Host.Contains("blob.core.windows.net"))
                {
                    result.StorageType = "Azure Blob Storage";
                    return await ValidateAzureBlobAccessibilityAsync(result, uri);
                }
                else if (uri.Host.Contains("file.core.windows.net"))
                {
                    result.StorageType = "Azure File Storage";
                    result.IsAccessible = false;
                    result.ErrorDetails = "Azure File Storage is not supported for WEBSITE_RUN_FROM_PACKAGE";
                    result.Recommendations.Add("Use Azure Blob Storage instead");
                    result.IsSuccessful = true;
                    return result;
                }
                else
                {
                    result.StorageType = "External URL";
                    return await ValidateExternalUrlAccessibilityAsync(result, uri);
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error validating package accessibility for {ResourceId}", resourceId);
                result.IsAccessible = false;
                result.IsSuccessful = false;
                result.ErrorDetails = "An error occurred during validation";
                result.Recommendations.Add("Check the package URL and try again");
                return result;
            }
        }

        /// <summary>
        /// Validates Azure Blob accessibility using managed identity when possible
        /// </summary>
        private async Task<PackageAccessibilityResult> ValidateAzureBlobAccessibilityAsync(PackageAccessibilityResult result, Uri blobUri)
        {
            try
            {
                // Use authentication service for validation
                var credential = await _authService.GetArmOperationCredential();
                
                try
                {
                    var blobClient = new BlobClient(blobUri, credential);
                    var properties = await blobClient.GetPropertiesAsync();
                    
                    result.IsAccessible = true;
                    result.RequiresAuthentication = true;
                    result.IsSuccessful = true;
                    result.ResponseCode = 200;
                    
                    _logger.LogInternalInformation("Successfully validated blob access using managed identity");
                    return result;
                }
                catch (Azure.RequestFailedException azureEx)
                {
                    _logger.LogInternalInformation("Managed identity access failed with status {StatusCode}: {Message}", azureEx.Status, azureEx.Message);
                    
                    // Check for specific error codes to differentiate between file not found and permissions issues
                    if (azureEx.Status == 404)
                    {
                        result.IsAccessible = false;
                        result.ErrorDetails = "The specified blob does not exist";
                        result.Recommendations.Add("Verify the blob name and container path are correct");
                        result.Recommendations.Add("Ensure the blob has been uploaded to the storage account");
                        result.IsSuccessful = true;
                        return result;
                    }
                    else if (azureEx.Status == 403)
                    {
                        // This is a permissions issue, try with SAS token if available
                        _logger.LogInternalInformation("Access forbidden with managed identity, trying SAS token");
                    }
                    else if (azureEx.Status == 401)
                    {
                        // Authentication failed, try with SAS token if available
                        _logger.LogInternalInformation("Authentication failed with managed identity, trying SAS token");
                    }
                    else
                    {
                        // Other Azure-specific error
                        result.IsAccessible = false;
                        result.ErrorDetails = $"Azure storage error: {azureEx.ErrorCode} - {azureEx.Message}";
                        result.Recommendations.Add("Check the blob URL and storage account configuration");
                        result.IsSuccessful = true;
                        return result;
                    }
                    
                    // If managed identity fails with 401/403, try with SAS token if present
                    if (blobUri.Query.Contains("sig="))
                    {
                        try
                        {
                            var blobClientWithSas = new BlobClient(blobUri);
                            var properties = await blobClientWithSas.GetPropertiesAsync();
                            
                            result.IsAccessible = true;
                            result.RequiresAuthentication = false; // SAS token provides access
                            result.IsSuccessful = true;
                            result.ResponseCode = 200;
                            
                            _logger.LogInternalInformation("Successfully validated blob access using SAS token");
                            return result;
                        }
                        catch (Azure.RequestFailedException sasAzureEx)
                        {
                            _logger.LogInternalWarning("SAS token access also failed with status {StatusCode}: {Message}", sasAzureEx.Status, sasAzureEx.Message);
                            
                            // Check for specific error codes with SAS token
                            if (sasAzureEx.Status == 404)
                            {
                                result.IsAccessible = false;
                                result.ErrorDetails = "The specified blob does not exist";
                                result.Recommendations.Add("Verify the blob name and container path are correct");
                                result.Recommendations.Add("Ensure the blob has been uploaded to the storage account");
                            }
                            else if (sasAzureEx.Status == 403)
                            {
                                result.IsAccessible = false;
                                result.ErrorDetails = "Access denied - SAS token does not have sufficient permissions";
                                result.Recommendations.Add("Ensure the SAS token has read permissions for the blob");
                                result.Recommendations.Add("Check SAS token expiration time");
                                result.Recommendations.Add("Verify the SAS token was generated for the correct blob/container");
                            }
                            else if (sasAzureEx.Status == 401)
                            {
                                result.IsAccessible = false;
                                result.ErrorDetails = "SAS token authentication failed";
                                result.Recommendations.Add("Check if the SAS token has expired");
                                result.Recommendations.Add("Verify the SAS token format is correct");
                                result.Recommendations.Add("Ensure the SAS token was generated for the correct storage account");
                            }
                            else
                            {
                                result.IsAccessible = false;
                                result.ErrorDetails = $"Azure storage error with SAS token: {sasAzureEx.ErrorCode} - {sasAzureEx.Message}";
                                result.Recommendations.Add("Check the SAS token and blob URL configuration");
                            }
                        }
                        catch (Exception sasEx)
                        {
                            _logger.LogInternalWarning("SAS token access failed with non-Azure exception: {Error}", sasEx.Message);
                            result.IsAccessible = false;
                            result.ErrorDetails = "Unable to access blob with SAS token";
                            result.Recommendations.Add("Check SAS token validity and network connectivity");
                        }
                    }
                    else
                    {
                        // No SAS token available and managed identity failed
                        if (azureEx.Status == 403)
                        {
                            result.IsAccessible = false;
                            result.RequiresAuthentication = true;
                            result.ErrorDetails = "Access denied - insufficient permissions";
                            result.Recommendations.Add("Ensure managed identity has Storage Blob Data Reader role");
                            result.Recommendations.Add("Or provide a valid SAS token in the URL");
                        }
                        else if (azureEx.Status == 401)
                        {
                            result.IsAccessible = false;
                            result.RequiresAuthentication = true;
                            result.ErrorDetails = "Authentication required - no valid credentials available";
                            result.Recommendations.Add("Ensure managed identity is properly configured");
                            result.Recommendations.Add("Or provide a valid SAS token in the URL");
                        }
                    }
                }
                catch (Exception managedIdentityEx)
                {
                    _logger.LogInternalInformation("Managed identity access failed with non-Azure exception: {Error}", managedIdentityEx.Message);
                    
                    // If managed identity fails with non-Azure exception, try with SAS token if present
                    if (blobUri.Query.Contains("sig="))
                    // If managed identity fails with non-Azure exception, try with SAS token if present
                    if (blobUri.Query.Contains("sig="))
                    {
                        try
                        {
                            var blobClientWithSas = new BlobClient(blobUri);
                            var properties = await blobClientWithSas.GetPropertiesAsync();
                            
                            result.IsAccessible = true;
                            result.RequiresAuthentication = false; // SAS token provides access
                            result.IsSuccessful = true;
                            result.ResponseCode = 200;
                            
                            _logger.LogInternalInformation("Successfully validated blob access using SAS token");
                            return result;
                        }
                        catch (Azure.RequestFailedException sasAzureEx)
                        {
                            _logger.LogInternalWarning("SAS token access also failed with Azure exception: {Status} - {Message}", sasAzureEx.Status, sasAzureEx.Message);
                            
                            if (sasAzureEx.Status == 404)
                            {
                                result.IsAccessible = false;
                                result.ErrorDetails = "The specified blob does not exist";
                                result.Recommendations.Add("Verify the blob name and container path are correct");
                                result.Recommendations.Add("Ensure the blob has been uploaded to the storage account");
                            }
                            else
                            {
                                result.IsAccessible = false;
                                result.ErrorDetails = $"Unable to access blob: {sasAzureEx.ErrorCode} - {sasAzureEx.Message}";
                                result.Recommendations.Add("Check SAS token validity and blob accessibility");
                            }
                        }
                        catch (Exception sasEx)
                        {
                            _logger.LogInternalWarning("SAS token access failed: {Error}", sasEx.Message);
                            result.IsAccessible = false;
                            result.ErrorDetails = "Unable to access blob with available credentials";
                            result.Recommendations.Add("Check SAS token validity and network connectivity");
                        }
                    }
                    else
                    {
                        result.IsAccessible = false;
                        result.RequiresAuthentication = true;
                        result.ErrorDetails = "Blob requires authentication and no valid credentials available";
                        result.Recommendations.Add("Ensure managed identity has Storage Blob Data Reader role");
                        result.Recommendations.Add("Or provide a valid SAS token in the URL");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error validating Azure Blob accessibility");
                result.IsAccessible = false;
                result.ErrorDetails = "Failed to validate blob accessibility";
                result.Recommendations.Add("Check blob URL and permissions");
            }

            result.IsSuccessful = true;
            return result;
        }

        /// <summary>
        /// Validates external URL accessibility without downloading content
        /// </summary>
        private async Task<PackageAccessibilityResult> ValidateExternalUrlAccessibilityAsync(PackageAccessibilityResult result, Uri uri)
        {
            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                // Use HEAD request to check accessibility without downloading content
                using var request = new HttpRequestMessage(HttpMethod.Head, uri);
                using var response = await httpClient.SendAsync(request);

                result.ResponseCode = (int)response.StatusCode;
                result.IsAccessible = response.IsSuccessStatusCode;
                result.RequiresAuthentication = response.StatusCode == System.Net.HttpStatusCode.Unauthorized;
                result.IsSuccessful = true;

                if (result.IsAccessible)
                {
                    _logger.LogInternalInformation("External URL is accessible: {StatusCode}", response.StatusCode);
                }
                else
                {
                    result.ErrorDetails = $"HTTP {response.StatusCode}: {response.ReasonPhrase}";
                    
                    switch (response.StatusCode)
                    {
                        case System.Net.HttpStatusCode.NotFound:
                            result.ErrorDetails = "The specified file does not exist at the given URL";
                            result.Recommendations.Add("Verify the URL is correct and the file exists");
                            result.Recommendations.Add("Check if the file has been uploaded to the correct location");
                            break;
                        case System.Net.HttpStatusCode.Unauthorized:
                            result.ErrorDetails = "Authentication required to access the URL";
                            result.Recommendations.Add("Provide authentication credentials or use a URL that allows anonymous access");
                            break;
                        case System.Net.HttpStatusCode.Forbidden:
                            result.ErrorDetails = "Access to the URL is forbidden";
                            result.Recommendations.Add("Check if you have permission to access this resource");
                            result.Recommendations.Add("Verify the access token or credentials if using authenticated URL");
                            break;
                        default:
                            result.ErrorDetails = $"HTTP {response.StatusCode}: {response.ReasonPhrase}";
                            result.Recommendations.Add($"Server returned {response.StatusCode}. Check the URL and server status");
                            break;
                    }
                }
            }
            catch (TaskCanceledException)
            {
                result.IsAccessible = false;
                result.ErrorDetails = "Request timed out";
                result.Recommendations.Add("URL may be unreachable or server is too slow");
                result.IsSuccessful = true;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error validating external URL accessibility");
                result.IsAccessible = false;
                result.ErrorDetails = "Network error occurred";
                result.Recommendations.Add("Check network connectivity and URL validity");
                result.IsSuccessful = true;
            }

            return result;
        }

        /// <summary>
        /// Generates a temporary SAS token for secure access to storage
        /// </summary>
        /// <param name="resourceId">The Azure resource ID</param>
        /// <param name="storageAccountName">The storage account name</param>
        /// <param name="containerName">The container name</param>
        /// <param name="blobName">The blob name</param>
        /// <returns>SAS URL generation result with sanitized output</returns>
        private async Task<SasUrlGenerationResult> GenerateSecureSasTokenAsync(string resourceId, string storageAccountName, string containerName, string blobName)
        {
            var result = new SasUrlGenerationResult
            {
                StorageAccountName = storageAccountName,
                ContainerName = containerName,
                BlobName = blobName
            };

            try
            {
                _logger.LogInternalInformation("Generating SAS token for {StorageAccount}/{Container}/{Blob}", 
                    storageAccountName, containerName, blobName);

                // Use authentication service for secure access
                var credential = await _authService.GetArmOperationCredential();
                var blobServiceClient = new BlobServiceClient(new Uri($"https://{storageAccountName}.blob.core.windows.net"), credential);
                var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                // Check if blob exists first
                var exists = await blobClient.ExistsAsync();
                if (!exists.Value)
                {
                    result.IsSuccessful = false;
                    result.ErrorMessage = "Blob does not exist";
                    return result;
                }

                // Generate SAS token with minimal permissions and short expiration
                var sasBuilder = new Azure.Storage.Sas.BlobSasBuilder
                {
                    BlobContainerName = containerName,
                    BlobName = blobName,
                    Resource = "b", // blob
                    ExpiresOn = DateTimeOffset.UtcNow.AddHours(1), // 1 hour expiration
                };

                // Only grant read permission
                sasBuilder.SetPermissions(Azure.Storage.Sas.BlobSasPermissions.Read);

                // Generate the SAS token
                var sasToken = blobClient.GenerateSasUri(sasBuilder);
                
                result.IsSuccessful = true;
                result.SasUrl = SanitizeUrl(sasToken.ToString()); // Sanitize for logging/output
                result.ExpiresAt = sasBuilder.ExpiresOn.DateTime;

                _logger.LogInternalInformation("Successfully generated SAS token for blob, expires at {ExpiresAt}", result.ExpiresAt);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error generating SAS token for {StorageAccount}/{Container}/{Blob}", 
                    storageAccountName, containerName, blobName);
                
                result.IsSuccessful = false;
                result.ErrorMessage = $"Failed to generate SAS token: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Validates storage connection without retrieving data
        /// </summary>
        /// <param name="resourceId">The Azure resource ID</param>
        /// <param name="storageConnectionInfo">Storage connection information (sanitized)</param>
        /// <returns>Connection validation result</returns>
        private async Task<bool> ValidateStorageConnectionAsync(string resourceId, string storageConnectionInfo)
        {
            try
            {
                _logger.LogInternalInformation("Validating storage connection for {ResourceId}", resourceId);

                // Parse connection info to determine storage type
                if (Uri.TryCreate(storageConnectionInfo, UriKind.Absolute, out Uri? uri))
                {
                    if (uri.Host.Contains("blob.core.windows.net"))
                    {
                        // Use authentication service for validation
                        var credential = await _authService.GetArmOperationCredential();
                        var blobClient = new BlobClient(uri, credential);
                        
                        try
                        {
                            // Just check if we can access the blob properties (no content download)
                            var properties = await blobClient.GetPropertiesAsync();
                            _logger.LogInternalInformation("Storage connection validation successful");
                            return true;
                        }
                        catch
                        {
                            // If managed identity fails, try with SAS if present
                            if (uri.Query.Contains("sig="))
                            {
                                var blobClientWithSas = new BlobClient(uri);
                                var properties = await blobClientWithSas.GetPropertiesAsync();
                                _logger.LogInternalInformation("Storage connection validation successful with SAS");
                                return true;
                            }
                            throw;
                        }
                    }
                }

                _logger.LogInternalWarning("Unable to validate storage connection - unsupported format");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Storage connection validation failed for {ResourceId}", resourceId);
                return false;
            }
        }

        #endregion

        /// <summary>
        /// Helper method to add a query parameter to a URI
        /// </summary>
        private string AddQueryParam(string uri, string name, string value)
        {
            if (uri.Contains('?'))
                return uri + $"&{name}={HttpUtility.UrlEncode(value)}";
            else
                return uri + $"?{name}={HttpUtility.UrlEncode(value)}";
        }

        /// <summary>
        /// Formats a byte size into a human-readable string
        /// </summary>
        private string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }
    }
}
