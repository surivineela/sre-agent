// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.DataConnectors;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.DataConnectors;

/// <summary>
/// Data connector that provisions MCP connections using the agent runtime infrastructure.
/// </summary>
[DataConnector("Mcp")]
public class McpDataConnector : IDataConnector
{
    private sealed record McpConnectionSettings(
        string TransportType,
        string? Endpoint,
        McpAuthenticationConfig? Authentication,
        Dictionary<string, string>? Headers,
        string? Description,
        string? ServiceType);

    private const string DefaultTransportType = "http";

    public string Endpoint { get; private set; } = string.Empty;
    public McpAuthenticationConfig? AuthenticationConfig { get; private set; }

    private readonly ILogger<McpDataConnector> _logger;
    private readonly IMcpConnectionEventManager _mcpConnectionManager;

    private DataConnectorInstanceSettings? _settings;
    private string? _connectionId;

    public McpDataConnector(
        ILogger<McpDataConnector> logger,
        IMcpConnectionEventManager mcpConnectionManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mcpConnectionManager = mcpConnectionManager ?? throw new ArgumentNullException(nameof(mcpConnectionManager));
    }

    /// <summary>
    /// How often this data connector should run to verify the MCP connection.
    /// </summary>
    public TimeSpan Interval => TimeSpan.FromMinutes(5);

    public async Task InitAsync(DataConnectorInstanceSettings instanceSettings, CancellationToken stoppingToken)
    {
        _settings = instanceSettings ?? throw new ArgumentNullException(nameof(instanceSettings));

        McpConnectionSettings parsedSettings = ParseDataSource(instanceSettings.Name, instanceSettings.DataSource);

        _logger.LogInternalInformation(
            "Initializing MCP connector '{Name}' with transport '{Transport}' and endpoint host '{Host}'",
            instanceSettings.Name,
            parsedSettings.TransportType,
            GetEndpointHost(parsedSettings.Endpoint));

        try
        {
            var connection = await _mcpConnectionManager.CreateAndAddConnectionAsync(
                name: instanceSettings.Name,
                type: parsedSettings.TransportType,
                endpoint: parsedSettings.Endpoint,
                authConfig: parsedSettings.Authentication,
                headers: parsedSettings.Headers,
                description: parsedSettings.Description,
                serviceType: parsedSettings.ServiceType);

            _connectionId = connection.Id;

            _logger.LogInternalInformation(
                "Successfully created MCP connection '{Name}' with status '{Status}' and {ToolCount} tools",
                instanceSettings.Name,
                connection.Status,
                connection.Tools?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(
                ex,
                "Failed to initialize MCP connector '{Name}': {Message}",
                instanceSettings.Name,
                ex.Message);
            throw;
        }
    }

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        if (_settings == null || string.IsNullOrEmpty(_connectionId))
        {
            _logger.LogInternalWarning("MCP connector not initialized, skipping run");
            return;
        }

        try
        {
            _logger.LogInternalInformation(
                "Verifying MCP connection '{Name}' (ID: {ConnectionId})",
                _settings.Name,
                _connectionId);

            var connection = await _mcpConnectionManager.GetConnectionAsync(_connectionId);

            if (connection != null)
            {
                _logger.LogInternalInformation(
                    "MCP connection '{Name}' is active with status '{Status}' and {ToolCount} tools",
                    _settings.Name,
                    connection.Status,
                    connection.Tools?.Count ?? 0);
            }
            else
            {
                _logger.LogInternalWarning(
                    "MCP connection '{Name}' (ID: {ConnectionId}) not found; it may have been removed",
                    _settings.Name,
                    _connectionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(
                ex,
                "Error verifying MCP connection '{Name}': {Message}",
                _settings.Name,
                ex.Message);
        }
    }

    private static McpConnectionSettings ParseDataSource(string connectionName, string dataSource)
    {
        if (string.IsNullOrWhiteSpace(dataSource))
        {
            throw new ArgumentException("DataSource cannot be null or empty for MCP connector.", nameof(dataSource));
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string segment in dataSource.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int separatorIndex = segment.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == segment.Length - 1)
            {
                continue;
            }

            string key = segment[..separatorIndex].Trim();
            string value = segment[(separatorIndex + 1)..].Trim();

            if (!string.IsNullOrEmpty(key))
            {
                values[key] = value;
            }
        }

        if (!values.TryGetValue("Endpoint", out string? endpoint) || string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException("DataSource must include an Endpoint value for MCP connector.", nameof(dataSource));
        }

        McpAuthenticationConfig? authentication = null;

        if (values.TryGetValue("AuthType", out string? authTypeValue) && !string.IsNullOrWhiteSpace(authTypeValue))
        {
            authentication = BuildAuthenticationConfig(authTypeValue, values);
        }

        var headerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Endpoint",
            "AuthType",
            "BearerToken"
        };

        Dictionary<string, string>? headers = null;

        foreach ((string key, string value) in values)
        {
            if (headerKeys.Contains(key))
            {
                continue;
            }

            headers ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            headers[key] = value;
        }

        return new McpConnectionSettings(DefaultTransportType, endpoint, authentication, headers, "Mcp Tool", connectionName);
    }

    // TODO: Extend to support other authentication types as needed.
    private static McpAuthenticationConfig? BuildAuthenticationConfig(string authTypeValue, IDictionary<string, string> values)
    {
        if (authTypeValue.Equals("BearerToken", StringComparison.OrdinalIgnoreCase) ||
            authTypeValue.Equals("Bearer", StringComparison.OrdinalIgnoreCase))
        {
            if (!values.TryGetValue("BearerToken", out string? bearerToken) || string.IsNullOrWhiteSpace(bearerToken))
            {
                throw new ArgumentException(
                    "DataSource AuthType 'BearerToken' requires a non-empty BearerToken entry.",
                    nameof(values));
            }

            return new McpAuthenticationConfig
            {
                Type = McpAuthenticationType.Bearer,
                BearerToken = bearerToken
            };
        }

        throw new NotSupportedException($"AuthType '{authTypeValue}' is not supported by the MCP data connector.");
    }

    private static string GetEndpointHost(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri))
        {
            return uri.Host;
        }

        return endpoint;
    }
}
