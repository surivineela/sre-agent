// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.Http;
using System.Text;
using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Framework;
using Agent.Logging;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.DataConnectors.TSG
{
    /// <summary>
    /// Azure DevOps REST client for TSG document crawling (supports both Git repositories and Wikis)
    /// </summary>
    public class TsgAzureDevOpsClient
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;
        private readonly AzureDevOpsSettings _settings;
        private readonly TokenCredential _tokenCredential;

        public TsgAzureDevOpsClient(
            ILogger logger,
            IHttpClientFactory httpClientFactory,
            AzureDevOpsSettings settings,
            ConnectorAuthSettings authSettings,
            IAuthenticationService authService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = httpClientFactory.CreateClient();
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _tokenCredential = authService.GetDataConnectorCredential(authSettings);
        }

        /// <summary>
        /// Gets the base URL for Wiki API calls
        /// </summary>
        private string GetWikiApiBaseUrl() =>
            $"https://dev.azure.com/{_settings.Organization}/{_settings.ProjectName}/_apis/wiki/wikis/{_settings.WikiIdentifier}";

        /// <summary>
        /// Gets the base URL for Git API calls
        /// </summary>
        private string GetGitApiBaseUrl() =>
            $"https://dev.azure.com/{_settings.Organization}/{_settings.ProjectName}/_apis/git/repositories/{_settings.RepositoryName}";

        /// <summary>
        /// Get all files/pages from the repository or wiki
        /// </summary>
        public async Task<List<string>> GetAllFilesAsync(string path)
        {
            return _settings.SourceType == AzureDevOpsSourceType.Wiki
                ? await GetAllWikiPagesAsync()
                : await GetAllGitFilesAsync(path);
        }

        /// <summary>
        /// Read file/page content from repository or wiki
        /// </summary>
        public async Task<string> ReadFileAsync(string filePath)
        {
            return _settings.SourceType == AzureDevOpsSourceType.Wiki
                ? await ReadWikiPageAsync(filePath)
                : await ReadGitFileAsync(filePath);
        }

        #region Git Repository Methods

        /// <summary>
        /// Get all files from a Git repository
        /// </summary>
        private async Task<List<string>> GetAllGitFilesAsync(string path)
        {
            var allFiles = new List<string>();

            try
            {
                var result = await ListGitFilesAsync(path, "Full");

                using (JsonDocument document = JsonDocument.Parse(result))
                {
                    JsonElement root = document.RootElement;
                    if (root.TryGetProperty("value", out JsonElement valueArray) && valueArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement item in valueArray.EnumerateArray())
                        {
                            bool isFolder = item.TryGetProperty("isFolder", out JsonElement isFolderElement) && isFolderElement.GetBoolean();
                            if (isFolder)
                            {
                                continue;
                            }

                            if (item.TryGetProperty("path", out JsonElement pathElement))
                            {
                                var filePath = pathElement.GetString();
                                if (filePath != null)
                                {
                                    allFiles.Add(filePath);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error retrieving files from path: {path}");
                throw;
            }

            return allFiles;
        }

        /// <summary>
        /// List files in a Git repository path
        /// </summary>
        private async Task<string> ListGitFilesAsync(string path, string recursionLevel)
        {
            try
            {
                var token = await GetAccessTokenAsync();

                // Use scopePath instead of path when using recursionLevel to get all items
                var url = $"{GetGitApiBaseUrl()}/items?scopePath={Uri.EscapeDataString(path)}&recursionLevel={recursionLevel}&includeContent=false&api-version=7.0";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", token);

                var response = await _httpClient.SendAsync(request);
                _logger.LogInternalInformation($"debug message using Bearer: Received response with status code: {response.Content}");
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error listing files from path: {path}");
                throw;
            }
        }

        /// <summary>
        /// Read file content from Git repository
        /// </summary>
        private async Task<string> ReadGitFileAsync(string filePath)
        {
            try
            {
                var token = await GetAccessTokenAsync();
                var url = $"{GetGitApiBaseUrl()}/items?path={Uri.EscapeDataString(filePath)}&includeContent=true&api-version=7.0";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", token);

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();

                // The responseContent is the actual file contents, not JSON
                return responseContent ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error reading file: {filePath}");
                throw;
            }
        }

        #endregion

        #region Wiki Methods

        /// <summary>
        /// Get all pages from an Azure DevOps Wiki, optionally scoped to a specific page and its subpages
        /// </summary>
        private async Task<List<string>> GetAllWikiPagesAsync()
        {
            var allPages = new List<string>();

            try
            {
                var token = await GetAccessTokenAsync();

                // Wiki API: GET /_apis/wiki/wikis/{wikiIdentifier}/pages?recursionLevel=Full
                // If WikiPageId is specified, first get that page to find its path, then use path parameter
                string url;
                if (_settings.WikiPageId.HasValue)
                {
                    // First, get the page by ID to retrieve its actual path
                    var pagePath = await GetWikiPagePathByIdAsync(_settings.WikiPageId.Value);
                    if (string.IsNullOrEmpty(pagePath))
                    {
                        _logger.LogInternalError($"Could not resolve wiki page path for page ID: {_settings.WikiPageId}");
                        return allPages;
                    }

                    var encodedPath = Uri.EscapeDataString(pagePath);
                    url = $"{GetWikiApiBaseUrl()}/pages?path={encodedPath}&recursionLevel=Full&api-version=7.0";
                    _logger.LogInternalInformation($"Scoped wiki crawl to page ID {_settings.WikiPageId} (path: {pagePath})");
                }
                else
                {
                    url = $"{GetWikiApiBaseUrl()}/pages?recursionLevel=Full&api-version=7.0";
                    _logger.LogInternalInformation("Crawling entire wiki (no page scope)");
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", token);

                var response = await _httpClient.SendAsync(request);
                _logger.LogInternalInformation($"Wiki pages API response status: {response.StatusCode}");
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();

                using (JsonDocument document = JsonDocument.Parse(responseContent))
                {
                    // The wiki pages API returns a tree structure with subPages
                    ExtractWikiPages(document.RootElement, allPages);
                }

                _logger.LogInternalInformation($"Discovered {allPages.Count} wiki pages");
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error retrieving wiki pages");
                throw;
            }

            return allPages;
        }

        /// <summary>
        /// Get the path of a wiki page by its ID
        /// </summary>
        private async Task<string?> GetWikiPagePathByIdAsync(int pageId)
        {
            try
            {
                var token = await GetAccessTokenAsync();

                // Wiki API: GET /_apis/wiki/wikis/{wikiIdentifier}/pages/{pageId}
                var url = $"{GetWikiApiBaseUrl()}/pages/{pageId}?api-version=7.0";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", token);

                var response = await _httpClient.SendAsync(request);
                _logger.LogInternalInformation($"Wiki page by ID API response status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogInternalError($"Failed to get wiki page by ID {pageId}: {response.StatusCode}");
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();

                using (JsonDocument document = JsonDocument.Parse(responseContent))
                {
                    if (document.RootElement.TryGetProperty("path", out JsonElement pathElement))
                    {
                        return pathElement.GetString();
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error getting wiki page path for ID {pageId}");
                return null;
            }
        }

        /// <summary>
        /// Recursively extract wiki page paths from the API response
        /// </summary>
        private void ExtractWikiPages(JsonElement element, List<string> pages)
        {
            // Extract path from current element
            if (element.TryGetProperty("path", out JsonElement pathElement))
            {
                var path = pathElement.GetString();
                if (!string.IsNullOrEmpty(path))
                {
                    pages.Add(path);
                }
            }

            // Recursively process subPages
            if (element.TryGetProperty("subPages", out JsonElement subPagesElement) && subPagesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement subPage in subPagesElement.EnumerateArray())
                {
                    ExtractWikiPages(subPage, pages);
                }
            }
        }

        /// <summary>
        /// Read wiki page content
        /// </summary>
        private async Task<string> ReadWikiPageAsync(string pagePath)
        {
            try
            {
                var token = await GetAccessTokenAsync();

                // Wiki API: GET /_apis/wiki/wikis/{wikiIdentifier}/pages?path={pagePath}&includeContent=true
                var encodedPath = Uri.EscapeDataString(pagePath);
                var url = $"{GetWikiApiBaseUrl()}/pages?path={encodedPath}&includeContent=true&api-version=7.0";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", token);

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();

                // Parse the JSON response to get the content field
                using (JsonDocument document = JsonDocument.Parse(responseContent))
                {
                    if (document.RootElement.TryGetProperty("content", out JsonElement contentElement))
                    {
                        return contentElement.GetString() ?? string.Empty;
                    }
                }

                _logger.LogInternalWarning($"Wiki page {pagePath} has no content");
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error reading wiki page: {pagePath}");
                throw;
            }
        }

        #endregion

        /// <summary>
        /// Get Azure DevOps access token using managed identity
        /// </summary>
        private async Task<string> GetAccessTokenAsync()
        {
            try
            {
                // Request a token for Azure DevOps scope
                var tokenRequestContext = new TokenRequestContext(new[] { "499b84ac-1321-427f-aa17-267ca6975798/.default" });
                var tokenResult = await _tokenCredential.GetTokenAsync(tokenRequestContext, CancellationToken.None);

                if (string.IsNullOrEmpty(tokenResult.Token))
                {
                    throw new InvalidOperationException("Failed to obtain Azure DevOps access token");
                }

                // Convert token to Base64 for Basic Authentication
                var tokenBytes = Encoding.ASCII.GetBytes($":{tokenResult.Token}");
                return Convert.ToBase64String(tokenBytes);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error getting Azure DevOps access token");
                throw;
            }
        }
    }
}
