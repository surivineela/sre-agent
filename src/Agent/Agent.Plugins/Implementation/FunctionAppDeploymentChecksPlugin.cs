using System.Net.Http.Headers;
using System.Web;
using System.Xml;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
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
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuthenticationService _authService;
        private readonly IRunFromPackagePlugin _runFromPackagePlugin;

        /// <summary>
        /// Gets or sets the thread ID
        /// </summary>
        public Guid? ThreadId { get; set; }

        /// <summary>
        /// Constructor for FunctionAppDeploymentChecksPlugin
        /// </summary>
        /// <param name="logger">Logger for the plugin</param>
        /// <param name="armHelper">ARM helper for interacting with Azure resources</param>
        /// <param name="httpClientFactory">HTTP client factory for making HTTP requests</param>
        /// <param name="authService">Authentication service for Azure resources</param>
        /// <param name="runFromPackagePlugin">Plugin for WEBSITE_RUN_FROM_PACKAGE operations</param>
        public FunctionAppDeploymentChecksPlugin(
            ILogger<FunctionAppDeploymentChecksPlugin> logger,
            ArmHelper armHelper,
            IHttpClientFactory httpClientFactory,
            IAuthenticationService authService,
            IRunFromPackagePlugin runFromPackagePlugin)
        {
            _logger = logger;
            _armHelper = armHelper;
            _httpClientFactory = httpClientFactory;
            _authService = authService;
            _runFromPackagePlugin = runFromPackagePlugin;
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
                if (!Uri.TryCreate(zipFilePath, UriKind.Absolute, out Uri? uri) ||
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

                var cred = await _authService.GetArmOperationCredential();
                var blobServiceClient = new BlobServiceClient(
                    new Uri($"https://{accountName}.blob.core.windows.net"),
                    cred);

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

        /// <summary>
        /// Lists blobs in a storage container using ARM REST API
        /// </summary>
        /// <param name="containerUri">The URI of the container to list blobs from, including any query parameters</param>
        /// <returns>A result containing the list of blobs in the container</returns>
        public async Task<StorageBlobListResult> ListStorageBlobsAsync(string containerUri)
        {
            var result = new StorageBlobListResult();

            try
            {
                _logger.LogInternalInformation("Listing blobs from container: {ContainerUri}", containerUri);

                // Validate the container URI
                if (string.IsNullOrWhiteSpace(containerUri))
                {
                    result.IsSuccessful = false;
                    result.ErrorMessage = "Container URI cannot be empty";
                    return result;
                }

                if (!Uri.TryCreate(containerUri, UriKind.Absolute, out Uri? uri))
                {
                    result.IsSuccessful = false;
                    result.ErrorMessage = $"The provided container URI is not valid: {containerUri}";
                    return result;
                }

                // Extract storage account and container name from the URI
                string storageAccountName = uri.Host.Split('.')[0];

                // Parse query string to look for the container name
                var queryParams = HttpUtility.ParseQueryString(uri.Query);
                string containerName = string.Empty;

                // Extract container name from URI path segments
                if (uri.Segments.Length > 1)
                {
                    containerName = uri.Segments[1].TrimEnd('/');
                }

                // Add comp=list parameter if missing and add additional required parameters
                string requestUri = containerUri;
                bool hasCompList = uri.Query.Contains("comp=list");
                bool hasRestype = uri.Query.Contains("restype=container");
                bool hasPrefix = uri.Query.Contains("prefix=");
                bool hasDelimiter = uri.Query.Contains("delimiter=");
                bool hasMaxResults = uri.Query.Contains("maxresults=");
                // Removed check for hasInclude as we're removing this parameter

                if (!hasCompList || !hasRestype || !hasPrefix || !hasDelimiter || !hasMaxResults)
                {
                    // Start with the base URI
                    var uriBuilder = new UriBuilder(uri);
                    var query = HttpUtility.ParseQueryString(uriBuilder.Query);

                    // Add or update required parameters
                    if (!hasCompList)
                        query["comp"] = "list";
                    if (!hasRestype)
                        query["restype"] = "container";
                    if (!hasPrefix)
                        query["prefix"] = "";
                    if (!hasDelimiter)
                        query["delimiter"] = "%2F"; // URL-encoded forward slash
                    if (!hasMaxResults)
                        query["maxresults"] = "100";
                    // Removed setting include=metadata parameter

                    // Update the URI with the new query string
                    uriBuilder.Query = query.ToString();
                    requestUri = uriBuilder.Uri.ToString();

                    _logger.LogInternalInformation("Updated request URI with required parameters: {RequestUri}", requestUri);
                }

                result.StorageAccountName = storageAccountName;
                result.ContainerName = containerName;
                result.ContainerUri = requestUri;

                // Create HTTP client for the request
                using var httpClient = _httpClientFactory.CreateClient();

                // Add required headers
                httpClient.DefaultRequestHeaders.Add("x-ms-client-session-id", "0113f3dab7784a05b285de727a9bcc72");
                httpClient.DefaultRequestHeaders.Add("x-ms-command-name", "StorageClient.ListBlobs");
                httpClient.DefaultRequestHeaders.Add("x-ms-date", DateTime.UtcNow.ToString("R"));
                httpClient.DefaultRequestHeaders.Add("x-ms-file-request-intent", "backup");
                httpClient.DefaultRequestHeaders.Add("x-ms-version", "2024-11-04");

                // Initialize collection for all blobs
                var allBlobs = new List<StorageBlobItem>();
                string nextMarker = string.Empty;
                bool hasMoreResults = true;

                // Loop to handle pagination
                while (hasMoreResults)
                {
                    // Construct request URI with marker if we have one
                    string pageRequestUri = requestUri;
                    if (!string.IsNullOrEmpty(nextMarker))
                    {
                        // Add the marker parameter
                        if (pageRequestUri.Contains('?'))
                            pageRequestUri += "&marker=" + HttpUtility.UrlEncode(nextMarker);
                        else
                            pageRequestUri += "?marker=" + HttpUtility.UrlEncode(nextMarker);

                        _logger.LogInternalInformation("Requesting next page with marker: {NextMarker}", nextMarker);
                    }

                    // Get ARM credentials
                    var cred = await _authService.GetArmOperationCredential();
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
                                _logger.LogInternalError("Storage API error response: {ErrorContent}",
                                    errorContent.Length <= 1000 ? errorContent : errorContent.Substring(0, 1000) + "...");
                                result.ErrorMessage += $" Error details: {errorContent}";
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInternalError(ex, "Failed to read error response content");
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
                        using var stringReader = new StringReader(xmlResponse);
                        using var xmlReader = XmlReader.Create(stringReader, new XmlReaderSettings
                        {
                            CheckCharacters = false,
                            IgnoreWhitespace = true,
                            IgnoreComments = true,
                            IgnoreProcessingInstructions = true,
                            DtdProcessing = DtdProcessing.Ignore  // Ignore DTD to prevent XXE attacks
                        });

                        // Debug logging to help diagnose the issue
                        _logger.LogInternalInformation("XML reader created, attempting to load document");

                        // Load the document with the reader
                        xmlDoc.Load(xmlReader);

                        _logger.LogInternalInformation("XML document loaded successfully");

                        // Verify we have a document element before proceeding
                        if (xmlDoc.DocumentElement == null)
                        {
                            result.IsSuccessful = false;
                            result.ErrorMessage = "Invalid XML response: document element is null";
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
                            _logger.LogInternalInformation("Attempting alternative XML parsing approach");
                            xmlDoc.LoadXml(xmlResponse);
                            _logger.LogInternalInformation("Alternative XML parsing successful");
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

                                    // Get the blob properties
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
        /// Verifies files in a blob container using ListStorageBlobsAsync. 
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

        /// <summary>
        /// Checks if the Function App has WEBSITE_RUN_FROM_PACKAGE configuration issues
        /// </summary>
        /// <param name="resourceId">The Azure resource ID of the Function App or Web App</param>
        /// <returns>True if there are WEBSITE_RUN_FROM_PACKAGE issues that require specialized handling</returns>
        public async Task<bool> HasRunFromPackageIssueAsync(string resourceId)
        {
            try
            {
                _logger.LogInternalInformation("Checking for WEBSITE_RUN_FROM_PACKAGE issues for {ResourceId}", resourceId);

                // Set the thread ID for the RunFromPackagePlugin
                _runFromPackagePlugin.ThreadId = this.ThreadId;

                // Use the specialized plugin to check for issues
                bool hasIssues = await _runFromPackagePlugin.HasRunFromPackageIssuesAsync(resourceId);

                _logger.LogInternalInformation("WEBSITE_RUN_FROM_PACKAGE issue check result for {ResourceId}: {HasIssues}", resourceId, hasIssues);
                return hasIssues;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error checking for WEBSITE_RUN_FROM_PACKAGE issues for {ResourceId}", resourceId);
                
                // In case of error, return true to trigger handoff for investigation
                // This ensures that potential issues are not missed due to unexpected errors
                return true;
            }
        }
    }
}
