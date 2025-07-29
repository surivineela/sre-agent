// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.Http;
using System.Text;
using System.Text.Json;
using Agent.Logging;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.DataConnectors.TSG
{
    /// <summary>
    /// Azure DevOps REST client for TSG document crawling
    /// </summary>
    public class TsgAzureDevOpsClient
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;
        private readonly AzureDevOpsSettings _settings;
        private readonly TokenCredential _tokenCredential;
        private readonly bool _isDevelopment;

        public TsgAzureDevOpsClient(
            ILogger logger,
            IHttpClientFactory httpClientFactory,
            AzureDevOpsSettings settings,
            IHostEnvironment hostEnvironment)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = httpClientFactory.CreateClient();
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _isDevelopment = hostEnvironment.IsDevelopment();
            
            // Use DefaultAzureCredential for managed identity authentication
            _tokenCredential = new DefaultAzureCredential();
        }

        /// <summary>
        /// Get all files from the repository
        /// </summary>
        public async Task<List<string>> GetAllFilesAsync(string path)
        {
            var allFiles = new List<string>();
            
            try
            {
                // Fix: Use "Full" as string parameter, not int.MaxValue
                var result = await ListFilesAsync(path, "Full");

                using (JsonDocument document = JsonDocument.Parse(result))
                {
                    JsonElement root = document.RootElement;
                    if (root.TryGetProperty("value", out JsonElement valueArray) && valueArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement item in valueArray.EnumerateArray())
                        {
                            // Only add files, not folders
                            if (!(item.TryGetProperty("isFolder", out JsonElement isFolderElement) && isFolderElement.GetBoolean()))
                            {
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
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error retrieving files from path: {path}");
                throw;
            }

            return allFiles;
        }

        /// <summary>
        /// List files in a repository path
        /// </summary>
        private async Task<string> ListFilesAsync(string path, string recursionLevel)
        {
            try
            {
                var token = await GetAccessTokenAsync();
                
                // Use scopePath instead of path when using recursionLevel to get all items
                var url = $"https://dev.azure.com/{_settings.Organization}/{_settings.ProjectName}/_apis/git/repositories/{_settings.RepositoryName}/items?scopePath={Uri.EscapeDataString(path)}&recursionLevel={recursionLevel}&includeContent=false&api-version=7.0";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", token);

                var response = await _httpClient.SendAsync(request);
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
        /// Read file content from repository
        /// </summary>
        public async Task<string> ReadFileAsync(string filePath)
        {
            try
            {
                var token = await GetAccessTokenAsync();
                var url = $"https://dev.azure.com/{_settings.Organization}/{_settings.ProjectName}/_apis/git/repositories/{_settings.RepositoryName}/items?path={Uri.EscapeDataString(filePath)}&includeContent=true&api-version=7.0";

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
