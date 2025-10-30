// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.Http;
using Agent.Logging;
using Agent.Runtime.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services.Mcp;

/// <summary>
/// Custom HttpClient factory that creates authenticated HttpClients for MCP connections.
/// This intercepts ALL HttpClient creation and adds authentication when needed.
/// </summary>
public class McpHttpClientFactory : IHttpClientFactory
{
    private readonly IHttpClientFactory _innerFactory;
    private readonly ILogger<McpHttpClientFactory> _logger;
    private readonly Dictionary<string, McpAuthenticationConfig> _authConfigs = new();

    public McpHttpClientFactory(
        IHttpClientFactory innerFactory,
        ILogger<McpHttpClientFactory> logger)
    {
        _innerFactory = innerFactory ?? throw new ArgumentNullException(nameof(innerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Register authentication configuration for a specific endpoint.
    /// </summary>
    public void RegisterAuthentication(Uri endpoint, McpAuthenticationConfig authConfig)
    {
        var key = GetEndpointKey(endpoint);
        _authConfigs[key] = authConfig;
        
        _logger.LogInternalInformation(
            "Registered {AuthType} authentication for endpoint: {Endpoint}",
            authConfig.Type,
            endpoint);
    }

    /// <summary>
    /// Unregister authentication for a specific endpoint.
    /// </summary>
    public void UnregisterAuthentication(Uri endpoint)
    {
        var key = GetEndpointKey(endpoint);
        if (_authConfigs.Remove(key))
        {
            _logger.LogInternalInformation(
                "Unregistered authentication for endpoint: {Endpoint}",
                endpoint);
        }
    }

    public HttpClient CreateClient(string name)
    {
        // Check if this client needs authentication
        // For MCP connections, we'll use a naming convention or store mapping
        if (_authConfigs.TryGetValue(name, out var authConfig))
        {
            _logger.LogInternalInformation(
                "Creating authenticated HttpClient with {AuthType} for: {ClientName}",
                authConfig.Type,
                name);

            var handler = new McpAuthenticatedHttpClientHandler(authConfig, _logger);
            return new HttpClient(handler);
        }

        // Fall back to standard client
        return _innerFactory.CreateClient(name);
    }

    private static string GetEndpointKey(Uri endpoint)
    {
        // Create a stable key from the endpoint URL
        return $"{endpoint.Scheme}://{endpoint.Host}:{endpoint.Port}{endpoint.AbsolutePath}".ToLowerInvariant();
    }
}
