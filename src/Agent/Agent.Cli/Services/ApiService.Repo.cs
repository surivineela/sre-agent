// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Cli.Helpers;

namespace Agent.Cli.Services;

/// <summary>
/// API methods for repository connector operations.
/// </summary>
public partial class ApiService : IDisposable
{
    #region Repo Connector API

    /// <summary>
    /// Request model for creating/updating a repository connector.
    /// </summary>
    public record RepoConnectorRequest
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("dataSource")]
        public string DataSource { get; init; } = string.Empty;

        [JsonPropertyName("personalAccessToken")]
        public string PersonalAccessToken { get; init; } = string.Empty;
    }

    /// <summary>
    /// Response model for repository connector operations.
    /// </summary>
    public record RepoConnectorResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("dataSource")]
        public string DataSource { get; init; } = string.Empty;

        [JsonPropertyName("repoType")]
        public string RepoType { get; init; } = "AzureDevOps";

        [JsonPropertyName("status")]
        public string Status { get; init; } = "Healthy";

        [JsonPropertyName("lastValidated")]
        public DateTime? LastValidated { get; init; }

        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; init; }

        [JsonPropertyName("cloneStatus")]
        public string CloneStatus { get; init; } = "NotStarted";

        [JsonPropertyName("lastSuccessfulSync")]
        public DateTime? LastSuccessfulSync { get; init; }

        [JsonPropertyName("localPath")]
        public string? LocalPath { get; init; }

        [JsonPropertyName("latestCommit")]
        public string? LatestCommit { get; init; }
    }

    /// <summary>
    /// Lists all repository connectors.
    /// </summary>
    /// <returns>List of connectors and any error message.</returns>
    public async Task<(List<RepoConnectorResponse> Result, string? Error)> ListRepoConnectorsAsync()
    {
        var resultList = new List<RepoConnectorResponse>();

        try
        {
            var (connectors, statusCode, errorMessage) = await MakeHttpRequestAsync<List<RepoConnectorResponse>>(
                HttpMethod.Get, "api/v1/connectors/tsgcrawler");

            if (errorMessage != null)
            {
                return (resultList, errorMessage);
            }

            if (connectors != null)
            {
                resultList.AddRange(connectors);
            }

            return (resultList, null);
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"ListRepoConnectors failed: {ex.Message}");
            return (resultList, $"Failed to list connectors: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets a repository connector by name.
    /// </summary>
    /// <param name="name">The connector name.</param>
    /// <returns>The connector and any error message.</returns>
    public async Task<(RepoConnectorResponse? Result, string? Error)> GetRepoConnectorAsync(string name)
    {
        try
        {
            var (connector, statusCode, errorMessage) = await MakeHttpRequestAsync<RepoConnectorResponse>(
                HttpMethod.Get, $"api/v1/connectors/tsgcrawler/{Uri.EscapeDataString(name)}");

            if (statusCode == HttpStatusCode.NotFound)
            {
                return (null, $"Connector '{name}' not found");
            }

            if (errorMessage != null)
            {
                return (null, errorMessage);
            }

            return (connector, null);
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"GetRepoConnector failed: {ex.Message}");
            return (null, $"Failed to get connector: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates or updates a repository connector.
    /// </summary>
    /// <param name="request">The connector request.</param>
    /// <returns>Success status, the created/updated connector, and any error message.</returns>
    public async Task<(bool Success, RepoConnectorResponse? Result, string? Error)> CreateOrUpdateRepoConnectorAsync(RepoConnectorRequest request)
    {
        try
        {
            var jsonContent = JsonSerializer.Serialize(request, _camelCaseJsonOptions);

            var (responseContent, statusCode, errorMessage) = await MakeHttpRequestAsync<string>(
                HttpMethod.Post, "api/v1/connectors/tsgcrawler", jsonContent);

            if (errorMessage != null)
            {
                // Try to extract error details from the response
                if (!string.IsNullOrWhiteSpace(responseContent))
                {
                    try
                    {
                        var errorObj = JsonDocument.Parse(responseContent);
                        if (errorObj.RootElement.TryGetProperty("error", out var errorProp))
                        {
                            var errorDetails = errorProp.GetString();
                            if (errorObj.RootElement.TryGetProperty("details", out var detailsProp))
                            {
                                errorDetails += $": {detailsProp.GetString()}";
                            }
                            return (false, null, errorDetails);
                        }
                    }
                    catch
                    {
                        // Ignore JSON parsing errors
                    }
                }
                return (false, null, errorMessage);
            }

            // Parse the response
            if (!string.IsNullOrWhiteSpace(responseContent))
            {
                try
                {
                    var connector = JsonSerializer.Deserialize<RepoConnectorResponse>(responseContent, _camelCaseJsonOptions);
                    return (true, connector, null);
                }
                catch (JsonException ex)
                {
                    DebugLogger.Debug("Exception", $"Failed to parse connector response: {ex.Message}");
                    return (true, null, null); // Success but couldn't parse response
                }
            }

            return (true, null, null);
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"CreateOrUpdateRepoConnector failed: {ex.Message}");
            return (false, null, $"Failed to create/update connector: {ex.Message}");
        }
    }

    /// <summary>
    /// Deletes a repository connector.
    /// </summary>
    /// <param name="name">The connector name.</param>
    /// <returns>Success status and any error message.</returns>
    public async Task<(bool Success, string? Error)> DeleteRepoConnectorAsync(string name)
    {
        try
        {
            var (content, statusCode, errorMessage) = await MakeHttpRequestAsync<string>(
                HttpMethod.Delete, $"api/v1/connectors/tsgcrawler/{Uri.EscapeDataString(name)}");

            // NoContent (204) is success
            if (statusCode == HttpStatusCode.NoContent)
            {
                return (true, null);
            }

            // NotFound (404) - connector doesn't exist
            if (statusCode == HttpStatusCode.NotFound)
            {
                return (false, $"Connector '{name}' not found");
            }

            if (errorMessage != null)
            {
                return (false, errorMessage);
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"DeleteRepoConnector failed: {ex.Message}");
            return (false, $"Failed to delete connector: {ex.Message}");
        }
    }

    #endregion
}
