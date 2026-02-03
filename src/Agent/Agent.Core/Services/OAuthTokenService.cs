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

            // Check if token is expired or about to expire
            if (token.ExpiresOn.HasValue && token.ExpiresOn.Value <= DateTime.UtcNow.Add(TokenExpirationBuffer))
            {
                _logger.LogExternalInformation("GitHub OAuth token is expired or expiring soon, attempting refresh");

                if (string.IsNullOrEmpty(token.RefreshToken))
                {
                    _logger.LogInternalWarning("GitHub OAuth token is expired but no refresh token is available");
                    return null;
                }

                // Check if refresh token itself is expired
                if (token.RefreshTokenExpiresOn.HasValue && token.RefreshTokenExpiresOn.Value <= DateTime.UtcNow)
                {
                    _logger.LogInternalWarning("GitHub OAuth refresh token is expired");
                    return null;
                }

                // Attempt to refresh the token
                var refreshedToken = await RefreshGitHubTokenAsync(token.RefreshToken);
                if (refreshedToken is not null)
                {
                    // Save the new token to database
                    await _threadRepository.CreateOrUpdateGitHubAccessTokenAsync(refreshedToken);
                    _logger.LogExternalInformation("GitHub OAuth token refreshed successfully");
                    return refreshedToken;
                }

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
        // TODO: implement in Auth Endpoint since client secret is required
        await Task.CompletedTask;
        return null;
    }

    /// <summary>
    /// Refreshes the GitHub OAuth token using the refresh token
    /// GitHub OAuth2 token endpoint documentation: https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps
    /// </summary>
    private async Task<GitHubAccessToken?> RefreshGitHubTokenAsync(string refreshToken)
    {
        // TODO: implement in Auth Endpoint since client secret is required
        await Task.CompletedTask;
        return null;
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

    /// <summary>
    /// Response from GitHub OAuth token endpoint
    /// </summary>
    private sealed record GitHubTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken = null,
        [property: JsonPropertyName("refresh_token_expires_in")] int? RefreshTokenExpiresIn = null,
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
