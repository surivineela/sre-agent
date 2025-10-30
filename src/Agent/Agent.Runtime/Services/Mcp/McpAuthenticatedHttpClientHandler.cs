// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Agent.Logging;
using Agent.Runtime.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services.Mcp;

/// <summary>
/// HTTP message handler that applies MCP authentication to outgoing requests.
/// Supports API Key, Bearer token, Basic authentication, and custom headers.
/// </summary>
public class McpAuthenticatedHttpClientHandler : HttpClientHandler
{
    private readonly McpAuthenticationConfig _authConfig;
    private readonly ILogger _logger;

    public McpAuthenticatedHttpClientHandler(McpAuthenticationConfig authConfig, ILogger logger)
    {
        _authConfig = authConfig ?? throw new ArgumentNullException(nameof(authConfig));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Apply authentication based on the configured type
        ApplyAuthentication(request);

        // Send the request
        return await base.SendAsync(request, cancellationToken);
    }

    private void ApplyAuthentication(HttpRequestMessage request)
    {
        if (_authConfig.Type == McpAuthenticationType.None)
        {
            return;
        }

        _logger.LogInternalDebug("Applying {AuthType} authentication to MCP request", _authConfig.Type);

        switch (_authConfig.Type)
        {
            case McpAuthenticationType.ApiKey:
                ApplyApiKeyAuthentication(request);
                break;

            case McpAuthenticationType.Bearer:
                ApplyBearerAuthentication(request);
                break;

            case McpAuthenticationType.Basic:
                ApplyBasicAuthentication(request);
                break;

            case McpAuthenticationType.CustomHeaders:
                ApplyCustomHeaders(request);
                break;

            case McpAuthenticationType.KeyVault:
                // Credentials should already be resolved
                if (!string.IsNullOrEmpty(_authConfig.BearerToken))
                {
                    ApplyBearerAuthentication(request);
                }
                else if (!string.IsNullOrEmpty(_authConfig.ApiKey))
                {
                    ApplyApiKeyAuthentication(request);
                }
                break;
        }
    }

    private void ApplyApiKeyAuthentication(HttpRequestMessage request)
    {
        if (string.IsNullOrEmpty(_authConfig.ApiKey))
        {
            _logger.LogInternalWarning("API Key is null or empty");
            return;
        }

        var headerValue = string.IsNullOrEmpty(_authConfig.ApiKeyPrefix)
            ? _authConfig.ApiKey
            : $"{_authConfig.ApiKeyPrefix} {_authConfig.ApiKey}";

        request.Headers.Remove(_authConfig.ApiKeyHeader);
        request.Headers.Add(_authConfig.ApiKeyHeader, headerValue);

        _logger.LogInternalDebug("Added API Key to header: {HeaderName}", _authConfig.ApiKeyHeader);
    }

    private void ApplyBearerAuthentication(HttpRequestMessage request)
    {
        if (string.IsNullOrEmpty(_authConfig.BearerToken))
        {
            _logger.LogInternalWarning("Bearer token is null or empty");
            return;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authConfig.BearerToken);
        _logger.LogInternalDebug("Added Bearer token to Authorization header");
    }

    private void ApplyBasicAuthentication(HttpRequestMessage request)
    {
        if (string.IsNullOrEmpty(_authConfig.Username) || string.IsNullOrEmpty(_authConfig.Password))
        {
            _logger.LogInternalWarning("Username or password is null or empty for Basic authentication");
            return;
        }

        var credentials = $"{_authConfig.Username}:{_authConfig.Password}";
        var encodedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
        
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encodedCredentials);
        _logger.LogInternalDebug("Added Basic authentication to Authorization header");
    }

    private void ApplyCustomHeaders(HttpRequestMessage request)
    {
        if (_authConfig.CustomHeaders == null || _authConfig.CustomHeaders.Count == 0)
        {
            _logger.LogInternalWarning("Custom headers are null or empty");
            return;
        }

        foreach (var header in _authConfig.CustomHeaders)
        {
            request.Headers.Remove(header.Key);
            request.Headers.Add(header.Key, header.Value);
            _logger.LogInternalDebug("Added custom header: {HeaderName}", header.Key);
        }
    }
}
