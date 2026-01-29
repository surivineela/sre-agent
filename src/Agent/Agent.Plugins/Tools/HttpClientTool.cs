// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Data.Tools;
using Agent.Framework;
using Agent.Plugins.Extensions;
using Azure.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Tools;

/// <summary>
/// Factory for creating HttpClientTool instances.
/// </summary>
[ToolType("HttpClientTool")]
public class HttpClientToolExecutorFactory : IYamlToolExecutorFactory
{
    public IYamlToolExecutor Create(YamlToolDefinitionBase definition, IServiceProvider serviceProvider)
    {
        var httpClientToolDefinition = (HttpClientToolDefinition)definition;
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var connectorResolver = serviceProvider.GetService<IConnectorResolver>();
        var authenticationService = serviceProvider.GetService<IAuthenticationService>();
        var logger = serviceProvider.GetRequiredService<ILogger<HttpClientTool>>();
        return new HttpClientTool(httpClientToolDefinition, httpClientFactory, connectorResolver, authenticationService, logger);
    }
}

/// <summary>
/// HTTP Client tool implementation that extends YamlToolExecutor.
/// Makes HTTP requests to external APIs and returns the response content.
/// Supports authentication via data connectors and placeholder substitution.
/// </summary>
public partial class HttpClientTool : YamlToolExecutor<HttpClientToolDefinition>
{
    private static readonly Dictionary<string, Func<Connector.TeamsApiHubConnector, string>> TeamsPlaceholders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["TeamsGroupId"] = c => c.GroupId,
            ["TeamsChannelId"] = c => c.ChannelId
        };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConnectorResolver? _connectorResolver;
    private readonly IAuthenticationService? _authenticationService;
    private readonly ILogger<HttpClientTool> _logger;

    public HttpClientTool(
        HttpClientToolDefinition definition,
        IHttpClientFactory httpClientFactory,
        IConnectorResolver? connectorResolver = null,
        IAuthenticationService? authenticationService = null,
        ILogger<HttpClientTool>? logger = null) : base(definition)
    {
        _httpClientFactory = httpClientFactory;
        _connectorResolver = connectorResolver;
        _authenticationService = authenticationService;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<object?> ExecuteAsync(string threadId, AIFunctionArguments parameters)
    {
        if (string.IsNullOrWhiteSpace(ToolDefinition.Url))
        {
            throw new ArgumentException("URL is not defined in the HttpClientToolDefinition.");
        }

        // Convert to dictionary for easier lookup
        var paramsDict = ConvertToStringDictionary(parameters);

        // Resolve connector if auth is specified
        DataConnectorBasicInfo? connectorInfo = null;
        if (ToolDefinition.Auth != null && !string.IsNullOrWhiteSpace(ToolDefinition.Auth.DataConnector) && _connectorResolver != null)
        {
            connectorInfo = _connectorResolver.GetAllDataConnectors()
                .FirstOrDefault(c => string.Equals(c.Name, ToolDefinition.Auth.DataConnector, StringComparison.OrdinalIgnoreCase));
        }

        // Process URL with placeholder substitution (including ##datasource##)
        var url = ReplacePlaceholders(ToolDefinition.Url, paramsDict, threadId, connectorInfo);

        // Process body with placeholder substitution if present
        string? body = null;
        if (!string.IsNullOrWhiteSpace(ToolDefinition.Body))
        {
            body = ReplacePlaceholders(ToolDefinition.Body, paramsDict, threadId, connectorInfo);
        }

        // Create HTTP client and request
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(ToolDefinition.TimeoutSeconds);

        using var request = new HttpRequestMessage(
            new HttpMethod(ToolDefinition.Method.ToUpperInvariant()),
            url);

        // Add body if present
        if (!string.IsNullOrWhiteSpace(body))
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        // Add headers with placeholder substitution
        if (ToolDefinition.Headers != null)
        {
            foreach (var header in ToolDefinition.Headers)
            {
                var headerValue = ReplacePlaceholders(header.Value, paramsDict, threadId, connectorInfo);
                request.Headers.TryAddWithoutValidation(header.Key, headerValue);
            }
        }

        // Handle authentication via connector if specified
        if (connectorInfo != null && _authenticationService != null)
        {
            var scope = ToolDefinition.Auth?.Scope;
            var accessToken = await GetAccessTokenAsync(connectorInfo, scope);
            if (!string.IsNullOrEmpty(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
        }

        // Log the final request details
        _logger.LogInternalInformation(
            "[HttpClientTool] Sending HTTP request. Method: {Method}, URL: {Url}",
            request.Method,
            url);

        if (request.Headers.Any())
        {
            var headersLog = string.Join(", ", request.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"));
            _logger.LogInternalInformation("[HttpClientTool] Request headers: {Headers}", headersLog);
        }

        if (!string.IsNullOrWhiteSpace(body))
        {
            _logger.LogInternalInformation("[HttpClientTool] Request body: {Body}", body);
        }

        // Execute the request
        var response = await httpClient.SendAsync(request);

        // Read response content
        var content = await response.Content.ReadAsStringAsync();

        // Return formatted response with status info
        if (response.IsSuccessStatusCode)
        {
            return content;
        }
        else
        {
            return $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {content}";
        }
    }

    /// <summary>
    /// Gets an access token using the connector's authentication settings.
    /// </summary>
    private async Task<string> GetAccessTokenAsync(DataConnectorBasicInfo connectorInfo, string? scope)
    {
        if (_authenticationService == null)
        {
            return string.Empty;
        }

        var auth = new ConnectorAuthSettings
        {
            AuthenticationType = connectorInfo.Source == DataConnectorSource.AgentSpace.ToString()
                ? ConnectorAuthType.AgentSpace
                : ConnectorAuthType.UAMI,
            ManagedIdentityResourceId = connectorInfo.Identity
        };

        var credential = _authenticationService.GetDataConnectorCredential(auth);

        // Use provided scope or default to Azure Management scope
        var tokenScope = !string.IsNullOrWhiteSpace(scope)
            ? scope
            : "https://management.core.windows.net/.default";

        var tokenRequest = new TokenRequestContext(new[] { tokenScope });
        var accessToken = await credential.GetTokenAsync(tokenRequest, CancellationToken.None);
        return accessToken.Token;
    }

    private string ReplacePlaceholders(string template, Dictionary<string, string> parameters, string threadId, DataConnectorBasicInfo? connectorInfo)
    {
        var result = template;
        var matches = PlaceholderRegex().Matches(result);

        // Pre-resolve TeamsApiHubConnector if connector is Teams type
        var teamsConnector = ResolveTeamsConnector(connectorInfo);

        foreach (Match match in matches)
        {
            var placeholder = match.Groups[0].Value;
            var key = match.Groups[1].Value;
            string? valueToReplace = null;

            // Special handling for datasource placeholder - use TeamsConnector.ConnectionRuntimeUrl if available, otherwise DataSource
            if (key.Equals("datasource", StringComparison.OrdinalIgnoreCase))
            {
                if (teamsConnector != null)
                {
                    // Use ConnectionRuntimeUrl from resolved Teams connector
                    valueToReplace = teamsConnector.ConnectionRuntimeUrl.TrimEnd('/');
                    _logger.LogInternalInformation(
                        "[HttpClientTool] Using TeamsConnector.ConnectionRuntimeUrl for ##datasource##: {Url}",
                        valueToReplace);
                }
                else if (connectorInfo != null && !string.IsNullOrWhiteSpace(connectorInfo.DataSource))
                {
                    // Validate that DataSource is a valid URL
                    if (!Uri.TryCreate(connectorInfo.DataSource, UriKind.Absolute, out var uri) ||
                        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    {
                        throw new ArgumentException($"The ##datasource## placeholder requires a valid HTTP/HTTPS URL. Got: '{connectorInfo.DataSource}'");
                    }
                    // Remove trailing slash if present for cleaner URL construction
                    valueToReplace = connectorInfo.DataSource.TrimEnd('/');
                }
                else
                {
                    throw new ArgumentException("The ##datasource## placeholder requires a valid connector with DataSource configured.");
                }
            }
            // Special handling for Teams placeholders - get from TeamsApiHubConnector
            else if (TeamsPlaceholders.TryGetValue(key, out var teamsPropertyAccessor))
            {
                valueToReplace = teamsPropertyAccessor(teamsConnector!);
                _logger.LogInternalInformation(
                    "[HttpClientTool] Resolved Teams placeholder '{Key}' with value '{Value}'",
                    key,
                    valueToReplace);
                if (string.IsNullOrWhiteSpace(valueToReplace))
                {
                    throw new ArgumentException($"The ##{key}## placeholder value is empty or not configured in the Teams connector.");
                }
            }
            // Special handling for threadId placeholder
            else if (key.Equals("threadId", StringComparison.OrdinalIgnoreCase))
            {
                valueToReplace = threadId;
            }
            // Special handling for agent_endpoint placeholder
            else if (key.Equals("agent_endpoint", StringComparison.OrdinalIgnoreCase))
            {
                valueToReplace = Environment.GetEnvironmentVariable("AGENT_ENDPOINT");
            }
            // Special handling for agent_name placeholder
            else if (key.Equals("agent_name", StringComparison.OrdinalIgnoreCase))
            {
                valueToReplace = Environment.GetEnvironmentVariable("AGENT_NAME");
            }
            // Regular argument handling
            else if (parameters.TryGetValue(key, out var rawValue))
            {
                valueToReplace = rawValue.Trim();
            }
            else
            {
                throw new ArgumentException($"Missing required argument: '{key}' for placeholder '{placeholder}'");
            }

            // Replace placeholder with value if we have one
            if (valueToReplace != null)
            {
                result = result.Replace(placeholder, valueToReplace);
            }
        }

        return result;
    }

    /// <summary>
    /// Resolves a TeamsApiHubConnector from the connector info.
    /// Returns null if the connector is null, resolver is unavailable, or connector is not a Teams connector.
    /// </summary>
    private Connector.TeamsApiHubConnector? ResolveTeamsConnector(DataConnectorBasicInfo? connectorInfo)
    {
        if (connectorInfo == null || _connectorResolver == null)
        {
            return null;
        }

        if (!connectorInfo.ConnectorType.Equals("Teams", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        _logger.LogInternalInformation(
            "[HttpClientTool] Resolving TeamsApiHubConnector. Name: {Name}, Type: {Type}, DataSource: {DataSource}",
            connectorInfo.Name,
            connectorInfo.ConnectorType,
            connectorInfo.DataSource);

        var teamsConnector = _connectorResolver!.GetConnectorFromSettings<Connector.TeamsApiHubConnector>(
            connectorInfo.Name,
            connectorInfo.ConnectorType,
            connectorInfo.DataSource);

        _logger.LogInternalInformation(
            "[HttpClientTool] TeamsApiHubConnector resolved. GroupId: {GroupId}, ChannelId: {ChannelId}, ConnectionRuntimeUrl: {ConnectionRuntimeUrl}",
            teamsConnector.GroupId,
            teamsConnector.ChannelId,
            teamsConnector.ConnectionRuntimeUrl);

        return teamsConnector;
    }

    [GeneratedRegex(@"##(.*?)##")]
    private static partial Regex PlaceholderRegex();
}
