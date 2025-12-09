// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.DataConnectors;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.DataConnectors;

/// <summary>
/// Data connector that provisions MCP connections using the agent runtime infrastructure.
/// </summary>
[DataConnector("Mcp")]
public partial class McpDataConnector : IDataConnector
{
    private abstract record McpConnectionSettings(
        McpTransportType TransportType,
        string? Description,
        string? ServiceType);

    private sealed record HttpMcpConnectionSettings(
        string Endpoint,
        McpAuthenticationConfig? Authentication,
        Dictionary<string, string>? Headers,
        string? Description,
        string? ServiceType) : McpConnectionSettings(McpTransportType.Http, Description, ServiceType);

    private sealed record StdioMcpConnectionSettings(
        string Command,
        string[]? Arguments,
        string? Description,
        string? ServiceType,
        Dictionary<string, string>? EnvVars) : McpConnectionSettings(McpTransportType.Stdio, Description, ServiceType);

    public string Endpoint { get; private set; } = string.Empty;
    public McpAuthenticationConfig? AuthenticationConfig { get; private set; }

    private readonly ILogger<McpDataConnector> _logger;
    private readonly IMcpConnectionEventManager _mcpConnectionManager;
    private readonly IToolFactory<AgentContext> _toolFactory;

    private DataConnectorInstanceSettings? _settings;
    private string? _connectionId;

    public McpDataConnector(
        ILogger<McpDataConnector> logger,
        IMcpConnectionEventManager mcpConnectionManager,
        IToolFactory<AgentContext> toolFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mcpConnectionManager = mcpConnectionManager ?? throw new ArgumentNullException(nameof(mcpConnectionManager));
        _toolFactory = toolFactory ?? throw new ArgumentNullException(nameof(toolFactory));
    }

    /// <summary>
    /// How often this data connector should run to verify the MCP connection.
    /// </summary>
    public TimeSpan Interval => TimeSpan.FromMinutes(5);

    public async Task InitAsync(DataConnectorInstanceSettings instanceSettings, CancellationToken stoppingToken)
    {
        _settings = instanceSettings ?? throw new ArgumentNullException(nameof(instanceSettings));

        // Priority: ExtendedPropertiesJson > ExtendedProperties > DataSource
        McpConnectionSettings parsedSettings;

        if (!string.IsNullOrWhiteSpace(instanceSettings.ExtendedPropertiesJson))
        {
            // Parse JSON string to dictionary
            var properties = ParseJsonToExtendedProperties(instanceSettings.ExtendedPropertiesJson);
            parsedSettings = ParseFromDictionary(instanceSettings.Name, properties);
        }
        else if (instanceSettings.ExtendedProperties != null)
        {
            parsedSettings = ParseFromDictionary(instanceSettings.Name, instanceSettings.ExtendedProperties);
        }
        else
        {
            parsedSettings = ParseDataSource(instanceSettings.Name, instanceSettings.DataSource);
        }

        var endpointHost = parsedSettings is HttpMcpConnectionSettings http ? GetEndpointHost(http.Endpoint) : "N/A";

        _logger.LogInternalInformation(
            "Initializing MCP connector '{Name}' with transport '{Transport}' and endpoint host '{Host}'",
            instanceSettings.Name,
            parsedSettings.TransportType,
            endpointHost);

        try
        {
            McpConnection connection = parsedSettings switch
            {
                HttpMcpConnectionSettings httpSettings => await _mcpConnectionManager.CreateAndAddConnectionAsync(
                    name: instanceSettings.Name,
                    type: McpTransportType.Http,
                    endpoint: httpSettings.Endpoint,
                    authConfig: httpSettings.Authentication,
                    headers: httpSettings.Headers,
                    description: httpSettings.Description,
                    serviceType: httpSettings.ServiceType),

                StdioMcpConnectionSettings stdioSettings => await _mcpConnectionManager.CreateAndAddConnectionAsync(
                    name: instanceSettings.Name,
                    type: McpTransportType.Stdio,
                    command: stdioSettings.Command,
                    arguments: stdioSettings.Arguments,
                    description: stdioSettings.Description,
                    serviceType: stdioSettings.ServiceType,
                    envVars: stdioSettings.EnvVars,
                    identity: instanceSettings.Identity),

                _ => throw new InvalidOperationException($"Unsupported connection settings type: {parsedSettings.GetType().Name}")
            };

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

                try
                {
                    // In case a tool has been added/removed/updated, refresh the tools.
                    await _toolFactory.RefreshMcpToolsAsync();
                    _logger.LogInternalDebug("Refreshed MCP tools after verification for connector '{Name}'", _settings.Name);
                }
                catch (Exception refreshEx)
                {
                    _logger.LogInternalError(refreshEx, "Failed refreshing MCP tools during verification for connector '{Name}'", _settings.Name);
                }
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

    /// <summary>
    /// DTO for HTTP MCP connection extended properties
    /// </summary>
    private sealed record HttpMcpConnectionDto(
        string? Type,
        string? Endpoint,
        string? AuthType,
        string? BearerToken);

    /// <summary>
    /// DTO for Stdio MCP connection extended properties
    /// </summary>
    private sealed record StdioMcpConnectionDto(
        string? Type,
        string? Command,
        string[]? Args,
        Dictionary<string, string>? Envs);

    private static McpConnectionSettings ParseFromDictionary(string connectionName, Dictionary<string, System.Text.Json.JsonElement> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        // Convert dictionary to JSON and deserialize with case-insensitive property matching
        var jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        string json = System.Text.Json.JsonSerializer.Serialize(properties);

        // First, deserialize to determine the type
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        string? typeString = doc.RootElement.TryGetProperty("type", out var typeElement) ||
                            doc.RootElement.TryGetProperty("Type", out typeElement)
            ? typeElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(typeString))
        {
            typeString = "http";  // backward compatibility default to HTTP
        }

        if (!Enum.TryParse<McpTransportType>(typeString, ignoreCase: true, out var type))
        {
            throw new NotSupportedException($"Transport Type '{typeString}' is not supported by the MCP data connector.");
        }

        if (type == McpTransportType.Http)
        {
            var dto = System.Text.Json.JsonSerializer.Deserialize<HttpMcpConnectionDto>(json, jsonOptions);
            if (dto == null)
            {
                throw new ArgumentException("Failed to deserialize HTTP MCP connection properties.");
            }

            if (string.IsNullOrWhiteSpace(dto.Endpoint))
            {
                throw new ArgumentException("Endpoint value is required for MCP connector with HTTP transport.");
            }

            McpAuthenticationConfig? authentication = null;
            if (!string.IsNullOrWhiteSpace(dto.AuthType))
            {
                // For authentication, we need all properties as strings (including custom headers)
                var stringValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in properties)
                {
                    string? stringValue = kvp.Value.ValueKind == System.Text.Json.JsonValueKind.String
                        ? kvp.Value.GetString()
                        : kvp.Value.GetRawText();
                    if (!string.IsNullOrEmpty(stringValue))
                    {
                        stringValues[kvp.Key] = stringValue;
                    }
                }
                authentication = BuildAuthenticationConfig(dto.AuthType, stringValues);
            }

            return new HttpMcpConnectionSettings(
                Endpoint: dto.Endpoint,
                Authentication: authentication,
                Headers: null,
                Description: "Mcp Tool",
                ServiceType: connectionName);
        }
        else if (type == McpTransportType.Stdio)
        {
            var dto = System.Text.Json.JsonSerializer.Deserialize<StdioMcpConnectionDto>(json, jsonOptions);
            if (dto == null)
            {
                throw new ArgumentException("Failed to deserialize Stdio MCP connection properties.");
            }

            if (string.IsNullOrWhiteSpace(dto.Command))
            {
                throw new ArgumentException("Command value is required for MCP connector with Stdio transport.");
            }

            return new StdioMcpConnectionSettings(
                Command: dto.Command,
                Arguments: dto.Args,
                Description: "Mcp Tool",
                ServiceType: connectionName,
                EnvVars: dto.Envs);
        }
        else
        {
            throw new NotSupportedException($"Transport Type '{type}' is not supported by the MCP data connector.");
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

        if (!values.TryGetValue("Type", out string? typeString) || string.IsNullOrWhiteSpace(typeString))
        {
            typeString = "http";  // backward compatibility default to HTTP
        }

        // Parse the type string to enum with case-insensitive matching
        if (!Enum.TryParse<McpTransportType>(typeString, ignoreCase: true, out var type))
        {
            throw new NotSupportedException($"Transport Type '{typeString}' is not supported by the MCP data connector.");
        }

        // Parse environment variables if provided
        Dictionary<string, string>? envVars = null;
        if (values.TryGetValue("EnvJson", out string? envJson) && !string.IsNullOrWhiteSpace(envJson))
        {
            try
            {
                envVars = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(envJson);
            }
            catch (System.Text.Json.JsonException ex)
            {
                throw new ArgumentException("EnvJson value is not a valid JSON dictionary of strings.", ex);
            }
        }

        if (type == McpTransportType.Http)
        {
            if (!values.TryGetValue("Endpoint", out string? endpoint) || string.IsNullOrWhiteSpace(endpoint))
            {
                throw new ArgumentException("Endpoint value is required for MCP connector with HTTP transport.");
            }

            McpAuthenticationConfig? authentication = null;

            if (values.TryGetValue("AuthType", out string? authTypeValue) && !string.IsNullOrWhiteSpace(authTypeValue))
            {
                authentication = BuildAuthenticationConfig(authTypeValue, values);
            }

            return new HttpMcpConnectionSettings(
                Endpoint: endpoint,
                Authentication: authentication,
                Headers: null,
                Description: "Mcp Tool",
                ServiceType: connectionName);
        }
        else if (type == McpTransportType.Stdio)
        {
            if (!values.TryGetValue("Command", out string? command) || string.IsNullOrWhiteSpace(command))
            {
                throw new ArgumentException("Command value is required for MCP connector with Stdio transport.");
            }
            string[]? arguments = null;
            if (values.TryGetValue("ArgsJson", out string? argsJson) && !string.IsNullOrWhiteSpace(argsJson))
            {
                try
                {
                    arguments = System.Text.Json.JsonSerializer.Deserialize<string[]>(argsJson);
                }
                catch (System.Text.Json.JsonException ex)
                {
                    throw new ArgumentException("ArgsJson value is not a valid JSON array of strings.", ex);
                }
            }
            return new StdioMcpConnectionSettings(
                Command: command,
                Arguments: arguments,
                Description: "Mcp Tool",
                ServiceType: connectionName,
                EnvVars: envVars);
        }
        else
        {
            throw new NotSupportedException($"Transport Type '{type}' is not supported by the MCP data connector.");
        }
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

        if (authTypeValue.Equals("CustomHeaders", StringComparison.OrdinalIgnoreCase))
        {
            var headerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Type",
                "Endpoint",
                "Command",
                "Args",
                "Envs",
                "AuthType",
                "BearerToken"
            };

            var customHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach ((string key, string value) in values)
            {
                if (headerKeys.Contains(key))
                {
                    continue;
                }

                customHeaders[key] = value;
            }

            return new McpAuthenticationConfig
            {
                Type = McpAuthenticationType.CustomHeaders,
                CustomHeaders = customHeaders
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

    private static Dictionary<string, System.Text.Json.JsonElement> ParseJsonToExtendedProperties(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var result = new Dictionary<string, System.Text.Json.JsonElement>();
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                result[property.Name] = property.Value.Clone();
            }
            return result;
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new ArgumentException("ExtendedPropertiesJson is not valid JSON.", ex);
        }
    }
}
