using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Agent.Core.Helpers;
using Agent.Logging;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Agent.Plugins.Implementation
{

    /// <summary>
    /// Implementation of the Function App Deployment Checks Plugin
    /// </summary>
    public class FunctionAppDeploymentChecksPlugin : IFunctionAppDeploymentChecksPlugin
    {
        private readonly ILogger<FunctionAppDeploymentChecksPlugin> _logger;
        private readonly ArmHelper _armHelper;

        /// <summary>
        /// Gets or sets the thread ID
        /// </summary>
        public Guid? ThreadId { get; set; }

        /// <summary>
        /// Constructor for FunctionAppDeploymentChecksPlugin
        /// </summary>
        /// <param name="logger">Logger for the plugin</param>
        /// <param name="armHelper">ARM helper for interacting with Azure resources</param>
        public FunctionAppDeploymentChecksPlugin(
            ILogger<FunctionAppDeploymentChecksPlugin> logger,
            ArmHelper armHelper)
        {
            _logger = logger;
            _armHelper = armHelper;
        }

        /// <summary>
        /// Gets Function App deployment information for a Function App
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App</param>
        /// <param name="startTime">Optional start time for the query (defaults to 1 hour ago)</param>
        /// <param name="endTime">Optional end time for the query (defaults to current time minus 15 minutes)</param>
        /// <returns>A summary of function app deployment information</returns>
        public async Task<string> GetFunctionAppDeploymentChecks(string resourceId, DateTime? startTime = null, DateTime? endTime = null)
        {
            try
            {
                _logger.LogInternalInformation("Getting Function App deployment information for {ResourceId}", resourceId);

                // Call GetDetectorResponseWithTime with the 'FunctionsDeploymentExternal' detector ID
                string result = await _armHelper.GetDetectorResponseWithTime(resourceId, "FunctionsDeploymentExternal", startTime, endTime);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error getting Function App deployment information for {ResourceId}", resourceId);
                throw;
            }
        }

        /// <summary>
        /// Gets Function App deployment history for a Function App
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App</param>
        /// <param name="startTime">Optional start time for the query (defaults to 1 hour ago)</param>
        /// <param name="endTime">Optional end time for the query (defaults to current time minus 15 minutes)</param>
        /// <returns>A detailed history of function app deployments</returns>
        public async Task<string> GetFunctionAppDeploymentHistory(string resourceId, DateTime? startTime = null, DateTime? endTime = null)
        {
            try
            {
                _logger.LogInternalInformation("Getting Function App deployment history for {ResourceId}", resourceId);

                // Call GetDetectorResponseWithTime with the 'FunctionAppDeployed' detector ID
                string result = await _armHelper.GetDetectorResponseWithTime(resourceId, "FunctionAppDeployed", startTime, endTime);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error getting Function App deployment history for {ResourceId}", resourceId);
                throw;
            }
        }

        /// <summary>
        /// Gets Function App slot swap information for a Function App
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App</param>
        /// <param name="startTime">Optional start time for the query (defaults to 1 hour ago)</param>
        /// <param name="endTime">Optional end time for the query (defaults to current time minus 15 minutes)</param>
        /// <returns>A detailed history of function app slot swap operations</returns>
        public async Task<string> GetFunctionAppSlotSwapHistory(string resourceId, DateTime? startTime = null, DateTime? endTime = null)
        {
            try
            {
                _logger.LogInternalInformation("Getting Function App slot swap history for {ResourceId}", resourceId);

                // Call GetDetectorResponseWithTime with the 'FunctionAppSlotSwaps' detector ID
                string result = await _armHelper.GetDetectorResponseWithTime(resourceId, "swap", startTime, endTime);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error getting Function App slot swap history for {ResourceId}", resourceId);
                throw;
            }
        }

        /// <summary>
        /// Gets detailed deployment failure analysis for a Function App
        /// Note: This tool only works for Windows SKUs
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App</param>
        /// <param name="startTime">Optional start time for the query (defaults to 1 hour ago)</param>
        /// <param name="endTime">Optional end time for the query (defaults to current time minus 15 minutes)</param>
        /// <returns>A detailed analysis of deployment failures and potential causes</returns>
        public async Task<string> GetFunctionAppDeploymentFailureAnalysis(string resourceId, DateTime? startTime = null, DateTime? endTime = null)
        {
            try
            {
                _logger.LogInternalInformation("Getting Function App deployment failure analysis for {ResourceId}", resourceId);

                // First check if this is a Windows app since this feature only works for Windows SKUs
                string os = await _armHelper.GetOperatingSystemAsync(resourceId);
                if (os != "Windows")
                {
                    return $"{{\"error\":{{\"code\":\"UnsupportedOS\",\"message\":\"Deployment failure analysis is only supported for Windows Function Apps. This Function App is running on {os}.\"}}}}";
                }

                // Call GetAnalysisWithTime with the 'DeploymentFailureAnalysis' detector ID
                string result = await _armHelper.GetAnalysisWithTime(resourceId, "DeploymentFailureAnalysis", startTime, endTime);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error getting Function App deployment failure analysis for {ResourceId}", resourceId);
                throw;
            }
        }

        /// <summary>
        /// Verifies if a zip file exists in Azure Storage
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <param name="zipFilePath">Optional path to the zip file. If not provided, the WEBSITE_RUN_FROM_PACKAGE app setting will be used</param>
        /// <returns>A verification result indicating if the zip file exists and details about the verification</returns>
        public async Task<ZipFileVerificationResult> VerifyZipFileExistsAsync(string resourceId, string zipFilePath = null)
        {
            var result = new ZipFileVerificationResult();

            try
            {
                _logger.LogInternalInformation("Verifying zip file existence for {ResourceId}, ZipFilePath: {ZipFilePath}",
                    resourceId, zipFilePath ?? "Not provided");

                // If zipFilePath is not provided, retrieve it from app settings
                if (string.IsNullOrWhiteSpace(zipFilePath))
                {
                    _logger.LogInternalInformation("Zip file path not provided. Retrieving from WEBSITE_RUN_FROM_PACKAGE app setting");

                    // Get app settings
                    string appSettingsJson = await _armHelper.GetAppSettings(resourceId);

                    if (string.IsNullOrWhiteSpace(appSettingsJson))
                    {
                        result.IsSuccessful = false;
                        result.ErrorMessage = "Failed to retrieve app settings";
                        return result;
                    }

                    // Parse app settings to get WEBSITE_RUN_FROM_PACKAGE value
                    var appSettings = JObject.Parse(appSettingsJson);
                    var properties = appSettings["properties"] as JObject;

                    if (properties == null || !properties.TryGetValue("WEBSITE_RUN_FROM_PACKAGE", StringComparison.OrdinalIgnoreCase, out var runFromPackageValue))
                    {
                        result.IsSuccessful = false;
                        result.ErrorMessage = "WEBSITE_RUN_FROM_PACKAGE app setting not found";
                        return result;
                    }

                    zipFilePath = runFromPackageValue?.ToString();

                    if (string.IsNullOrWhiteSpace(zipFilePath) || zipFilePath == "0" || zipFilePath == "1" || zipFilePath == "true")
                    {
                        result.IsSuccessful = false;
                        result.ErrorMessage = $"WEBSITE_RUN_FROM_PACKAGE has an invalid value: {zipFilePath}. Expected a URL to a zip file.";
                        return result;
                    }
                }

                // Store the path we're verifying
                result.VerifiedPath = zipFilePath;

                // Validate that the path is a proper URL
                if (!Uri.TryCreate(zipFilePath, UriKind.Absolute, out Uri uri) ||
                    (uri.Scheme != "http" && uri.Scheme != "https"))
                {
                    result.IsSuccessful = false;
                    result.ErrorMessage = $"The provided path is not a valid URL: {zipFilePath}";
                    return result;
                }

                // Extract blob URI components for Azure Storage
                if (!TryParseBlobUri(uri, out string accountName, out string containerName, out string blobPath))
                {
                    result.IsSuccessful = false;
                    result.ErrorMessage = $"The provided URL is not a valid Azure Storage blob URL: {zipFilePath}";
                    return result;
                }

                // Connect to the Azure Storage account and check if the blob exists
                _logger.LogInternalInformation("Checking if blob exists: Account: {AccountName}, Container: {ContainerName}, Blob: {BlobPath}",
                    accountName, containerName, blobPath);

                var blobServiceClient = new BlobServiceClient(
                    new Uri($"https://{accountName}.blob.core.windows.net"),
                    new DefaultAzureCredential());

                var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobPath);

                // Check if the blob exists
                var exists = await blobClient.ExistsAsync();

                result.IsSuccessful = exists.Value;

                if (!exists.Value)
                {
                    result.ErrorMessage = $"The zip file was not found at the specified location: {zipFilePath}";
                }
                else
                {
                    // If the blob exists, get some additional details
                    var properties = await blobClient.GetPropertiesAsync();
                    result.Details = JsonSerializer.Serialize(new
                    {
                        Size = properties.Value.ContentLength,
                        ContentType = properties.Value.ContentType,
                        LastModified = properties.Value.LastModified.ToString("o"),
                        ETag = properties.Value.ETag.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error verifying zip file existence for {ResourceId}", resourceId);
                result.IsSuccessful = false;
                result.ErrorMessage = $"An error occurred while verifying the zip file: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Updates the WEBSITE_RUN_FROM_PACKAGE app setting to a new Azure Storage zip file path
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <param name="zipFilePath">Path to the zip file in Azure Storage</param>
        /// <returns>A result indicating the success or failure of the update operation</returns>
        public async Task<WebsiteRunFromPackageUpdateResult> UpdateWebsiteRunFromPackageAsync(string resourceId, string zipFilePath)
        {
            var result = new WebsiteRunFromPackageUpdateResult
            {
                ResourceId = resourceId,
                ZipFilePath = zipFilePath
            };

            try
            {
                _logger.LogInternalInformation("Updating WEBSITE_RUN_FROM_PACKAGE for {ResourceId} to {ZipFilePath}", resourceId, zipFilePath);

                // Validate the resource ID
                if (string.IsNullOrWhiteSpace(resourceId))
                {
                    result.IsSuccessful = false;
                    result.ErrorMessage = "Resource ID cannot be empty";
                    return result;
                }

                // Validate that the zip file path is provided
                if (string.IsNullOrWhiteSpace(zipFilePath))
                {
                    result.IsSuccessful = false;
                    result.ErrorMessage = "Zip file path cannot be empty";
                    return result;
                }

                // Validate that the path is a proper URL
                if (!Uri.TryCreate(zipFilePath, UriKind.Absolute, out Uri uri) ||
                    (uri.Scheme != "http" && uri.Scheme != "https"))
                {
                    result.IsSuccessful = false;
                    result.ErrorMessage = $"The provided path is not a valid URL: {zipFilePath}";
                    return result;
                }

                // Extract blob URI components for Azure Storage
                if (!TryParseBlobUri(uri, out string accountName, out string containerName, out string blobPath))
                {
                    result.IsSuccessful = false;
                    result.ErrorMessage = $"The provided URL is not a valid Azure Storage blob URL: {zipFilePath}";
                    return result;
                }

                // Connect to the Azure Storage account and check if the blob exists
                _logger.LogInternalInformation("Checking if blob exists: Account: {AccountName}, Container: {ContainerName}, Blob: {BlobPath}",
                    accountName, containerName, blobPath);

                var blobServiceClient = new BlobServiceClient(
                    new Uri($"https://{accountName}.blob.core.windows.net"),
                    new DefaultAzureCredential());

                var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobPath);

                // Check if the blob exists
                var exists = await blobClient.ExistsAsync();

                if (!exists.Value)
                {
                    result.IsSuccessful = false;
                    result.ErrorMessage = $"The zip file was not found at the specified location: {zipFilePath}";
                    return result;
                }

                // Verify that the blob is a zip file (check extension or content type)
                var blobProperties = await blobClient.GetPropertiesAsync();
                bool isZipFile = blobPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || 
                                 blobProperties.Value.ContentType == "application/zip" ||
                                 blobProperties.Value.ContentType == "application/x-zip-compressed";

                if (!isZipFile)
                {
                    result.IsSuccessful = false;
                    result.ErrorMessage = $"The file does not appear to be a zip file: {zipFilePath}";
                    return result;
                }

                // Get current app settings
                string appSettingsJson = await _armHelper.GetAppSettings(resourceId);

                if (string.IsNullOrWhiteSpace(appSettingsJson))
                {
                    result.IsSuccessful = false;
                    result.ErrorMessage = "Failed to retrieve app settings";
                    return result;
                }

                // Parse app settings to prepare for update
                var appSettings = new Dictionary<string, string>();
                var appSettingsObj = JObject.Parse(appSettingsJson);
                var properties = appSettingsObj["properties"] as JObject;

                if (properties == null)
                {
                    result.IsSuccessful = false;
                    result.ErrorMessage = "Failed to parse app settings";
                    return result;
                }

                // Check if there's an existing WEBSITE_RUN_FROM_PACKAGE setting
                bool existingSettingFound = false;
                string existingValue = string.Empty;

                if (properties.TryGetValue("WEBSITE_RUN_FROM_PACKAGE", StringComparison.OrdinalIgnoreCase, out var runFromPackageValue))
                {
                    existingSettingFound = true;
                    existingValue = runFromPackageValue?.ToString() ?? string.Empty;
                }

                // Create new app settings dictionary with the renamed and new values
                if (existingSettingFound && !string.IsNullOrEmpty(existingValue))
                {
                    appSettings["SREAGENT_RENAMED_WEBSITE_RUN_FROM_PACKAGE"] = existingValue;
                    result.Details += $"Renamed existing WEBSITE_RUN_FROM_PACKAGE value '{existingValue}' to SREAGENT_RENAMED_WEBSITE_RUN_FROM_PACKAGE. ";
                }

                // Set the new WEBSITE_RUN_FROM_PACKAGE value
                appSettings["WEBSITE_RUN_FROM_PACKAGE"] = zipFilePath;

                // Update app settings
                bool updateResult = await _armHelper.UpdateAppSettingsAsync(resourceId, appSettings);

                if (!updateResult)
                {
                    result.IsSuccessful = false;
                    result.ErrorMessage = "Failed to update app settings";
                    return result;
                }

                result.IsSuccessful = true;
                result.Details += $"Successfully updated WEBSITE_RUN_FROM_PACKAGE to '{zipFilePath}'.";
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error updating WEBSITE_RUN_FROM_PACKAGE for {ResourceId}", resourceId);
                result.IsSuccessful = false;
                result.ErrorMessage = $"An error occurred while updating WEBSITE_RUN_FROM_PACKAGE: {ex.Message}";
                return result;
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
    }
}
