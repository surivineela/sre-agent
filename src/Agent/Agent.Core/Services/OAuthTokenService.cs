// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services;

/// <summary>
/// Service for managing OAuth tokens including retrieval and refresh
/// </summary>
public sealed class OAuthTokenService : IOAuthTokenService, IDisposable
{
    private readonly IThreadRepository _threadRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OAuthTokenService> _logger;
    private readonly SemaphoreSlim _gitHubLock = new(1, 1);
    private readonly SemaphoreSlim _azureDevOpsLock = new(1, 1);
    private static readonly TimeSpan TokenExpirationBuffer = TimeSpan.FromMinutes(5);
    private bool _disposed;

    public OAuthTokenService(
        IThreadRepository threadRepository,
        IHttpClientFactory httpClientFactory,
        ILogger<OAuthTokenService> logger)
    {
        _threadRepository = threadRepository ?? throw new ArgumentNullException(nameof(threadRepository));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<GitHubAccessToken?> GetValidGitHubTokenAsync()
    {
        await _gitHubLock.WaitAsync();
        try
        {
            var token = await _threadRepository.GetGitHubAccessTokenAsync();
            if (token is null)
            {
                _logger.LogInternalWarning("No GitHub OAuth token found in database");
                return null;
            }

            // GitHub tokens currently don't support refresh - they use device flow
            // Check if token is expired
            if (token.ExpiresOn.HasValue && token.ExpiresOn.Value <= DateTime.UtcNow.Add(TokenExpirationBuffer))
            {
                _logger.LogInternalWarning("GitHub OAuth token is expired and cannot be refreshed automatically");
                return null;
            }

            return token;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error retrieving GitHub OAuth token");
            return null;
        }
        finally
        {
            _gitHubLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<AzureDevOpsAccessToken?> GetValidAzureDevOpsTokenAsync(string organizationName)
    {
        await _azureDevOpsLock.WaitAsync();
        try
        {
            var token = await _threadRepository.GetAzureDevOpsOAuthTokenAsync(organizationName);
            if (token is null)
            {
                _logger.LogInternalWarning("No Azure DevOps OAuth token found for organization {OrganizationName}", organizationName);
                return null;
            }

            // Check if token is expired or about to expire
            if (token.ExpiresOn.HasValue && token.ExpiresOn.Value <= DateTime.UtcNow.Add(TokenExpirationBuffer))
            {
                _logger.LogExternalInformation("Azure DevOps OAuth token for organization {OrganizationName} is expired or expiring soon, attempting refresh", organizationName);

                if (string.IsNullOrEmpty(token.RefreshToken))
                {
                    _logger.LogInternalWarning("Azure DevOps OAuth token for organization {OrganizationName} is expired but no refresh token is available", organizationName);
                    return null;
                }

                // Attempt to refresh the token
                var refreshedToken = await RefreshAzureDevOpsTokenAsync(token.RefreshToken);
                if (refreshedToken is not null)
                {
                    // Save the new token to database
                    await _threadRepository.CreateOrUpdateAzureDevOpsOAuthTokenAsync(refreshedToken, organizationName);
                    _logger.LogExternalInformation("Azure DevOps OAuth token for organization {OrganizationName} refreshed successfully", organizationName);
                    return refreshedToken;
                }

                _logger.LogInternalWarning("Failed to refresh Azure DevOps OAuth token for organization {OrganizationName}", organizationName);
                return null;
            }

            return token;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error retrieving Azure DevOps OAuth token for organization {OrganizationName}", organizationName);
            return null;
        }
        finally
        {
            _azureDevOpsLock.Release();
        }
    }

    /// <summary>
    /// Refreshes the Azure DevOps OAuth token using the refresh token
    /// Azure AD OAuth2 token endpoint documentation: https://learn.microsoft.com/en-us/azure/active-directory/develop/v2-oauth2-auth-code-flow#refresh-the-access-token
    /// </summary>
    private async Task<AzureDevOpsAccessToken?> RefreshAzureDevOpsTokenAsync(string refreshToken)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();

            // Azure AD token endpoint (common tenant since we support multi-tenant)
            var tokenEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/token";

            // Prepare the request parameters
            var requestParams = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = "499b84ac-1321-427f-aa17-267ca6975798", // Azure DevOps client ID
                ["scope"] = "499b84ac-1321-427f-aa17-267ca6975798/.default offline_access"
            };

            using var requestContent = new FormUrlEncodedContent(requestParams);
            using var response = await httpClient.PostAsync(tokenEndpoint, requestContent);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogInternalWarning(
                    "Failed to refresh Azure DevOps OAuth token. Status: {StatusCode}, Error: {Error}",
                    response.StatusCode,
                    errorContent);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenResponse = responseContent.DeserializeNoThrow<AzureDevOpsTokenResponse>();

            if (tokenResponse is null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                _logger.LogInternalWarning("Invalid token response from Azure DevOps refresh endpoint");
                return null;
            }

            var expiresOn = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
            return new AzureDevOpsAccessToken(
                tokenResponse.AccessToken,
                expiresOn,
                tokenResponse.RefreshToken ?? refreshToken); // Use new refresh token if provided, else keep old one
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error refreshing Azure DevOps OAuth token");
            return null;
        }
    }

    /// <summary>
    /// Response from Azure DevOps OAuth token endpoint
    /// </summary>
    private sealed record AzureDevOpsTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken = null,
        [property: JsonPropertyName("token_type")] string? TokenType = null,
        [property: JsonPropertyName("scope")] string? Scope = null);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _gitHubLock.Dispose();
        _azureDevOpsLock.Dispose();
        _disposed = true;
    }
}
