// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.Http.Headers;
using System.Text;
using Agent.Runtime.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services.Mcp;

/// <summary>
/// Interface for applying authentication to MCP requests.
/// </summary>
public interface IMcpAuthenticationService
{
    /// <summary>
    /// Applies authentication headers to an HttpClient based on the authentication configuration.
    /// </summary>
    Task ApplyAuthenticationAsync(HttpClient httpClient, McpAuthenticationConfig? authConfig);

    /// <summary>
    /// Resolves credentials from Key Vault if configured.
    /// </summary>
    Task<McpAuthenticationConfig> ResolveCredentialsAsync(McpAuthenticationConfig authConfig);
}

/// <summary>
/// Service for applying authentication to MCP server requests.
/// </summary>
public class McpAuthenticationService : IMcpAuthenticationService
{
    private readonly ILogger<McpAuthenticationService> _logger;

    public McpAuthenticationService(ILogger<McpAuthenticationService> logger)
    {
        _logger = logger;
    }

    public async Task ApplyAuthenticationAsync(HttpClient httpClient, McpAuthenticationConfig? authConfig)
    {
        if (authConfig == null || authConfig.Type == McpAuthenticationType.None)
        {
            return;
        }

        _logger.LogInternalDebug("Applying {AuthType} authentication to MCP request", authConfig.Type);

        // Resolve from Key Vault if needed
        if (!authConfig.IsResolved && authConfig.Type == McpAuthenticationType.KeyVault)
        {
            authConfig = await ResolveCredentialsAsync(authConfig);
        }

        switch (authConfig.Type)
        {
            case McpAuthenticationType.ApiKey:
                ApplyApiKeyAuthentication(httpClient, authConfig);
                break;

            case McpAuthenticationType.Bearer:
                ApplyBearerAuthentication(httpClient, authConfig);
                break;

            case McpAuthenticationType.Basic:
                ApplyBasicAuthentication(httpClient, authConfig);
                break;

            case McpAuthenticationType.CustomHeaders:
                ApplyCustomHeaders(httpClient, authConfig);
                break;

            case McpAuthenticationType.KeyVault:
                // Credentials should be resolved by now, apply them
                if (!string.IsNullOrEmpty(authConfig.BearerToken))
                {
                    ApplyBearerAuthentication(httpClient, authConfig);
                }
                else if (!string.IsNullOrEmpty(authConfig.ApiKey))
                {
                    ApplyApiKeyAuthentication(httpClient, authConfig);
                }
                break;
        }

        _logger.LogInternalDebug("Authentication applied successfully");
    }

    public Task<McpAuthenticationConfig> ResolveCredentialsAsync(McpAuthenticationConfig authConfig)
    {
        if (authConfig.Type != McpAuthenticationType.KeyVault || string.IsNullOrEmpty(authConfig.KeyVaultSecretUri))
        {
            return Task.FromResult(authConfig);
        }

        try
        {
            _logger.LogInternalInformation("Resolving credentials from Key Vault: {SecretUri}", authConfig.KeyVaultSecretUri);

            // TODO: Implement Azure Key Vault integration
            // For now, this is a placeholder that would be implemented when Key Vault support is added
            // Example implementation:
            // var credential = new DefaultAzureCredential();
            // var secretClient = new SecretClient(new Uri(vaultUri), credential);
            // var secret = await secretClient.GetSecretAsync(secretName);
            // authConfig.BearerToken = secret.Value.Value;

            _logger.LogInternalWarning("Key Vault integration not yet implemented. Using placeholder credentials.");

            authConfig.IsResolved = true;
            return Task.FromResult(authConfig);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to resolve credentials from Key Vault: {SecretUri}", authConfig.KeyVaultSecretUri);
            throw;
        }
    }

    private void ApplyApiKeyAuthentication(HttpClient httpClient, McpAuthenticationConfig authConfig)
    {
        if (string.IsNullOrEmpty(authConfig.ApiKey))
        {
            _logger.LogInternalWarning("API Key is null or empty");
            return;
        }

        var headerValue = string.IsNullOrEmpty(authConfig.ApiKeyPrefix)
            ? authConfig.ApiKey
            : $"{authConfig.ApiKeyPrefix} {authConfig.ApiKey}";

        httpClient.DefaultRequestHeaders.Remove(authConfig.ApiKeyHeader);
        httpClient.DefaultRequestHeaders.Add(authConfig.ApiKeyHeader, headerValue);

        _logger.LogInternalDebug("Added API Key to header: {HeaderName}", authConfig.ApiKeyHeader);
    }

    private void ApplyBearerAuthentication(HttpClient httpClient, McpAuthenticationConfig authConfig)
    {
        if (string.IsNullOrEmpty(authConfig.BearerToken))
        {
            _logger.LogInternalWarning("Bearer token is null or empty");
            return;
        }

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authConfig.BearerToken);
        _logger.LogInternalDebug("Added Bearer token to Authorization header");
    }

    private void ApplyBasicAuthentication(HttpClient httpClient, McpAuthenticationConfig authConfig)
    {
        if (string.IsNullOrEmpty(authConfig.Username) || string.IsNullOrEmpty(authConfig.Password))
        {
            _logger.LogInternalWarning("Username or password is null or empty for Basic authentication");
            return;
        }

        var credentials = $"{authConfig.Username}:{authConfig.Password}";
        var encodedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedCredentials);
        _logger.LogInternalDebug("Added Basic authentication to Authorization header");
    }

    private void ApplyCustomHeaders(HttpClient httpClient, McpAuthenticationConfig authConfig)
    {
        if (authConfig.CustomHeaders == null || authConfig.CustomHeaders.Count == 0)
        {
            _logger.LogInternalWarning("Custom headers are null or empty");
            return;
        }

        foreach (var header in authConfig.CustomHeaders)
        {
            httpClient.DefaultRequestHeaders.Remove(header.Key);
            httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            _logger.LogInternalDebug("Added custom header: {HeaderName}", header.Key);
        }
    }
}

/// <summary>
/// No-op implementation when authentication is disabled.
/// </summary>
public class NoOpMcpAuthenticationService : IMcpAuthenticationService
{
    public Task ApplyAuthenticationAsync(HttpClient httpClient, McpAuthenticationConfig? authConfig)
    {
        return Task.CompletedTask;
    }

    public Task<McpAuthenticationConfig> ResolveCredentialsAsync(McpAuthenticationConfig authConfig)
    {
        return Task.FromResult(authConfig);
    }
}
