// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Runtime.Models;
using Azure.Core;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace Agent.Runtime.Services.Mcp;

/// <summary>
/// Factory for creating authenticated HTTP client transports for MCP connections using StreamableHttp mode.
/// Supports ApiKey, Bearer, Basic, CustomHeaders, and AzureARM authentication via HttpClientTransportOptions.AdditionalHeaders.
/// </summary>
public static class AuthenticatedHttpClientTransportFactory
{
    /// <summary>
    /// Creates an HTTP transport with authentication support (async version for AzureARM auth).
    /// </summary>
    public static async Task<IClientTransport> CreateHttpTransportAsync(
        string name,
        Uri endpoint,
        McpAuthenticationConfig? authConfig,
        IAuthenticationService authService,
        ILogger? logger)
    {
        logger?.LogInternalInformation(
            "Creating HTTP transport (StreamableHttp mode) for '{Name}' to {Endpoint} with auth: {HasAuth}",
            name,
            endpoint,
            authConfig != null);

        var options = new HttpClientTransportOptions
        {
            Name = name,
            Endpoint = endpoint,
            TransportMode = HttpTransportMode.StreamableHttp
        };

        // Use AdditionalHeaders to inject authentication headers
        if (authConfig != null && authConfig.Type != McpAuthenticationType.None)
        {
            options.AdditionalHeaders = await CreateAuthenticationHeadersAsync(authConfig, authService, logger);

            logger?.LogInternalInformation(
                "Configured {AuthType} authentication via AdditionalHeaders ({Count} headers)",
                authConfig.Type,
                options.AdditionalHeaders.Count);
        }

        return new HttpClientTransport(options);
    }

    /// <summary>
    /// Creates authentication headers dictionary based on the authentication configuration (async version).
    /// Supports AzureARM authentication that requires async token acquisition.
    /// </summary>
    public static async Task<Dictionary<string, string>> CreateAuthenticationHeadersAsync(
        McpAuthenticationConfig authConfig,
        IAuthenticationService authService,
        ILogger? logger)
    {
        var headers = new Dictionary<string, string>();

        logger?.LogInternalInformation("Configuring authentication headers for {AuthType}", authConfig.Type);

        switch (authConfig.Type)
        {
            case McpAuthenticationType.ApiKey:
                if (!string.IsNullOrEmpty(authConfig.ApiKey))
                {
                    // Check if a specific header name is provided via CustomHeaders
                    if (authConfig.CustomHeaders?.ContainsKey("X-API-Key") == true)
                    {
                        headers["X-API-Key"] = authConfig.ApiKey;
                        logger?.LogInternalInformation("Added X-API-Key header");
                    }
                    else
                    {
                        // Default to Authorization: ApiKey header
                        headers["Authorization"] = $"ApiKey {authConfig.ApiKey}";
                        logger?.LogInternalInformation("Added Authorization: ApiKey header");
                    }
                }
                break;

            case McpAuthenticationType.Bearer:
                if (!string.IsNullOrEmpty(authConfig.BearerToken))
                {
                    headers["Authorization"] = $"Bearer {authConfig.BearerToken}";
                    logger?.LogInternalInformation("Added Authorization: Bearer header");
                }
                break;

            case McpAuthenticationType.Basic:
                if (!string.IsNullOrEmpty(authConfig.Username) && !string.IsNullOrEmpty(authConfig.Password))
                {
                    var credentials = Convert.ToBase64String(
                        System.Text.Encoding.ASCII.GetBytes($"{authConfig.Username}:{authConfig.Password}"));
                    headers["Authorization"] = $"Basic {credentials}";
                    logger?.LogInternalInformation("Added Authorization: Basic header");
                }
                break;

            case McpAuthenticationType.CustomHeaders:
                if (authConfig.CustomHeaders != null)
                {
                    foreach (var header in authConfig.CustomHeaders)
                    {
                        headers[header.Key] = header.Value;
                    }
                    logger?.LogInternalInformation("Added {Count} custom headers", authConfig.CustomHeaders.Count);
                }
                break;

            case McpAuthenticationType.AzureARM:
                // Get ARM credentials from auth service
                var armScope = authConfig.ArmScope ?? "https://management.azure.com/.default";
                logger?.LogInternalInformation("Acquiring Azure ARM token with scope: {Scope}", armScope);

                try
                {
                    // TODO: Use identity attached to mcp connector
                    var credential = await authService.GetArmOperationCredential();
                    var token = await credential.GetTokenAsync(
                        new TokenRequestContext(new[] { armScope }),
                        CancellationToken.None);

                    headers["Authorization"] = $"Bearer {token.Token}";
                    logger?.LogInternalInformation(
                        "Added Authorization: Bearer header (Azure ARM token, expires: {ExpiresOn})",
                        token.ExpiresOn);
                }
                catch (Exception ex)
                {
                    logger?.LogInternalError(ex, "Failed to acquire Azure ARM token");
                    throw new InvalidOperationException("Failed to acquire Azure ARM authentication token", ex);
                }
                break;
        }

        return headers;
    }
}
