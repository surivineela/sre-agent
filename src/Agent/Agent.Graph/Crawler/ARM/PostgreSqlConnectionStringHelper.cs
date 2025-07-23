// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Agent.Data.DatabaseClients.GraphDbClient;
using Azure.Core;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public enum PostgreSqlConnectionFormat
{
    URL,                // postgresql://, postgres://, jdbc:postgresql://
    SemicolonList,      // .NET/Npgsql format with semicolons
    KeyValueList,       // libpq conninfo format with spaces
    ServiceName,        // DSN/service file reference
    JSONPayload,        // JSON object with connection properties
    IndividualVars,     // Split PG* environment variables
    ODBC,               // ODBC connection string format
    Unknown
}

public class PostgreSqlConnectionStringHelper
{
    private readonly ILogger _logger;
    private readonly ArmClient _armClient;
    private readonly IGraphDatabaseClient _graphDbClient;
    private const string azurePostgreSqlSuffix = ".postgres.database.azure.com";

    public PostgreSqlConnectionStringHelper(ILogger logger, ArmClient armClient, IGraphDatabaseClient graphDbClient)
    {
        _logger = logger;
        _armClient = armClient;
        _graphDbClient = graphDbClient;
    }

    public async Task<ArmResourceNode?> GetPostgreSqlResourceFromConnectionStringAsync(
        GraphNode workloadNode,
        string value,
        string sourceType,
        string sourceName)
    {
        try
        {
            var parsedData = ParsePostgreSqlConnectionString(value, sourceName);
            if (parsedData == null || parsedData.Host == null) return null;

            var rawHost = parsedData.Host;
            var database = parsedData.Database;
            var serverName = rawHost;
            int portIndex = serverName.IndexOf(":");
            if (portIndex > 0)
            {
                serverName = serverName.Substring(0, portIndex);
            }

            var serverBaseName = serverName;
            if (serverBaseName.EndsWith(azurePostgreSqlSuffix, StringComparison.OrdinalIgnoreCase))
            {
                serverBaseName = serverBaseName.Substring(0, serverBaseName.Length - azurePostgreSqlSuffix.Length);
            }

            _logger.LogDebug($"Parsed PostgreSQL server name: {serverName}, Database: {database}");

            var subscription = _armClient.GetSubscriptionResource(new ResourceIdentifier("/subscriptions/" + workloadNode.GetSubscriptionId()));

            // Check for flexible servers first
            await foreach (var server in subscription.GetGenericResourcesAsync(filter: $"resourceType eq 'Microsoft.DBforPostgreSQL/flexibleServers' and name eq '{serverBaseName.ToLowerInvariant()}'"))
            {
                return await CreatePostgreSqlNode(workloadNode, server, parsedData, sourceType, sourceName, "Microsoft.DBforPostgreSQL/flexibleServers");
            }

            // Check for single servers
            await foreach (var server in subscription.GetGenericResourcesAsync(filter: $"resourceType eq 'Microsoft.DBforPostgreSQL/servers' and name eq '{serverBaseName.ToLowerInvariant()}'"))
            {
                return await CreatePostgreSqlNode(workloadNode, server, parsedData, sourceType, sourceName, "Microsoft.DBforPostgreSQL/servers");
            }

            // Check for Cosmos DB for PostgreSQL (formerly Hyperscale)
            await foreach (var server in subscription.GetGenericResourcesAsync(filter: $"resourceType eq 'Microsoft.DBforPostgreSQL/serverGroupsv2' and name eq '{serverBaseName.ToLowerInvariant()}'"))
            {
                return await CreatePostgreSqlNode(workloadNode, server, parsedData, sourceType, sourceName, "Microsoft.DBforPostgreSQL/serverGroupsv2");
            }

            _logger.LogInternalWarning($"PostgreSQL server with name {serverName} was not found in the subscription.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"Error processing PostgreSQL connection string: {ex.Message}");
            return null;
        }
    }
    public bool IsPostgreSqlConnectionString(string value, string? keyName = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        // Strip potential Azure App Service quotes
        value = value.Trim().Trim('"');

        // Early rejection of SQL Server patterns
        if (value.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Integrated Security=", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("AttachDbFilename=", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Connect Timeout=", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Trusted_Connection=", StringComparison.OrdinalIgnoreCase))
            return false;

        // Check for Azure connection string prefixes in key name
        if (keyName != null &&
            (keyName.StartsWith("POSTGRESQLCONNSTR_", StringComparison.OrdinalIgnoreCase) ||
             keyName.StartsWith("CUSTOMCONNSTR_", StringComparison.OrdinalIgnoreCase) ||
             keyName.Contains("POSTGRESQL", StringComparison.OrdinalIgnoreCase) ||
             keyName.Contains("POSTGRES", StringComparison.OrdinalIgnoreCase)))
            return true;

        // URI formats (including Python driver variants)
        if (value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("psql://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("postgresql+", StringComparison.OrdinalIgnoreCase))
            return true;

        // JDBC format
        if (value.StartsWith("jdbc:postgresql://", StringComparison.OrdinalIgnoreCase))
            return true;

        // ODBC format
        if (value.Contains("DRIVER={PostgreSQL", StringComparison.OrdinalIgnoreCase))
            return true;

        // Service name detection (simple word with appropriate key)
        if (Regex.IsMatch(value, @"^\w+$") &&
            (keyName?.Equals("PGSERVICE", StringComparison.OrdinalIgnoreCase) == true ||
             keyName?.Equals("service", StringComparison.OrdinalIgnoreCase) == true ||
             value.Contains("service=", StringComparison.OrdinalIgnoreCase)))
            return true;

        // JSON format detection
        var trimmed = value.Trim();
        if ((trimmed.StartsWith("{") && trimmed.EndsWith("}")) ||
            (trimmed.StartsWith("[") && trimmed.EndsWith("]")))
        {
            // Additional validation for PostgreSQL-specific JSON keys
            return trimmed.Contains("\"host\"") || trimmed.Contains("\"dbname\"") ||
                   trimmed.Contains("\"database\"") || trimmed.Contains("postgres");
        }

        // Azure-specific patterns (even without other indicators)
        if (value.Contains(".postgres.database.azure.com", StringComparison.OrdinalIgnoreCase))
            return true;

        // Key-value format with PostgreSQL indicators
        if ((value.Contains("host=", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("dbname=", StringComparison.OrdinalIgnoreCase)) &&
            (value.Contains(".postgres.database.azure.com", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("postgresql", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("port=5432", StringComparison.OrdinalIgnoreCase)))
            return true;
        // Single parameter checks for common PostgreSQL parameters
        if (value.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Server=", StringComparison.OrdinalIgnoreCase))
        {
            // Ensure it's not a SQL Server connection string by checking for absence of SQL Server indicators
            if (!value.Contains("Integrated Security", StringComparison.OrdinalIgnoreCase) &&
                !value.Contains("Trusted_Connection", StringComparison.OrdinalIgnoreCase) &&
                !value.Contains("Data Source", StringComparison.OrdinalIgnoreCase))
            {
                // Also ensure the host/server has a non-empty value
                var parts = value.Split(';', '=', ' ');
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    if ((parts[i].Trim().Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                         parts[i].Trim().Equals("Server", StringComparison.OrdinalIgnoreCase)) &&
                        !string.IsNullOrWhiteSpace(parts[i + 1]))
                    {
                        return true;
                    }
                }
            }
        }
        // Semicolon-separated format (Npgsql/.NET style) with stricter validation
        if (value.Contains(';') && value.Contains('=') &&
            value.Count(c => c == ';') >= value.Count(c => c == ' '))
        {
            // Must contain PostgreSQL-specific indicators and not SQL Server indicators
            var hasPostgreSqlIndicators =
                value.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("Username=", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("SSL Mode=", StringComparison.OrdinalIgnoreCase);

            var hasSqlServerIndicators =
                value.Contains("Integrated Security", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("Trusted_Connection", StringComparison.OrdinalIgnoreCase);

            // For database+username combinations (common PostgreSQL pattern), allow without explicit host
            var hasDatabaseAndUser =
                value.Contains("Database=", StringComparison.OrdinalIgnoreCase) &&
                (value.Contains("Username=", StringComparison.OrdinalIgnoreCase) ||
                 value.Contains("User ID=", StringComparison.OrdinalIgnoreCase));

            // Must have connection target (host/server) OR be a database+user combination
            var hasConnectionTarget =
                (value.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
                 value.Contains("Server=", StringComparison.OrdinalIgnoreCase)) &&
                !ContainsEmptyValue(value, "Host") &&
                !ContainsEmptyValue(value, "Server");

            return (hasPostgreSqlIndicators || hasDatabaseAndUser) && !hasSqlServerIndicators &&
                   (hasConnectionTarget || hasDatabaseAndUser);
        }
        return false;
    }

    private static bool ContainsEmptyValue(string connectionString, string parameterName)
    {
        var patterns = new[]
        {
            $"{parameterName}=;",
            $"{parameterName}= ;",
            $"{parameterName}=\"\";",
            $"{parameterName}='';",
            $"{parameterName}=\"\" ",
            $"{parameterName}='' "
        };

        foreach (var pattern in patterns)
        {
            if (connectionString.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Check for parameter at end with empty value
        if (connectionString.EndsWith($"{parameterName}=", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public PostgreSqlConnectionFormat DetectFormat(string value, string? keyName = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return PostgreSqlConnectionFormat.Unknown;

        // Strip potential Azure App Service quotes
        value = value.Trim().Trim('"');

        // URL formats
        if (value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("jdbc:postgresql://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("postgresql+", StringComparison.OrdinalIgnoreCase))
            return PostgreSqlConnectionFormat.URL;

        // JSON format
        var trimmed = value.Trim();
        if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
            return PostgreSqlConnectionFormat.JSONPayload;

        // ODBC format
        if (value.Contains("DRIVER={PostgreSQL", StringComparison.OrdinalIgnoreCase))
            return PostgreSqlConnectionFormat.ODBC;

        // Service name (simple word)
        if (Regex.IsMatch(value, @"^\w+$") &&
            (keyName?.Equals("PGSERVICE", StringComparison.OrdinalIgnoreCase) == true ||
             keyName?.Equals("service", StringComparison.OrdinalIgnoreCase) == true))
            return PostgreSqlConnectionFormat.ServiceName;

        // Semicolon list (typical for .NET/Npgsql)
        if (value.Contains(';') && value.Contains('=') &&
            value.Count(c => c == ';') >= value.Count(c => c == ' '))
            return PostgreSqlConnectionFormat.SemicolonList;
        // Key-value list (libpq conninfo)
        if (value.Contains("host=", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("dbname=", StringComparison.OrdinalIgnoreCase))
            return PostgreSqlConnectionFormat.KeyValueList;

        // Single key-value pair (might be incomplete but still recognizable format)
        if (value.Contains('=') && !value.Contains(';') &&
            (value.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("Database=", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("Server=", StringComparison.OrdinalIgnoreCase)))
            return PostgreSqlConnectionFormat.SemicolonList;  // Treat single param as semicolon format

        return PostgreSqlConnectionFormat.Unknown;
    }

    private async Task<ArmResourceNode> CreatePostgreSqlNode(
        GraphNode workloadNode,
        Azure.ResourceManager.Resources.GenericResource server,
        PostgreSqlConnectionData connectionData,
        string sourceType,
        string sourceName,
        string resourceType)
    {
        var postgresResourceId = new ResourceIdentifier(server.Data.Id.ToString());
        if (postgresResourceId is null)
        {
            _logger.LogInternalError(sourceName, $"PostgreSqlResourceId cannot be null for resource type {resourceType}.");
            throw new ArgumentNullException(nameof(postgresResourceId), "PostgreSqlResourceId cannot be null.");
        }            

        // Create PostgreSqlFlexServerNode for flexible servers, ArmResourceNode for others
        ArmResourceNode postgresNode;
        if (resourceType == "Microsoft.DBforPostgreSQL/flexibleServers")
        {
            postgresNode = new PostgreSqlFlexServerNode(
                resourceType: resourceType,
                resourceId: postgresResourceId!,
                subscriptionId: postgresResourceId.SubscriptionId!,
                resourceGroupName: postgresResourceId.ResourceGroupName!,
                resourceName: postgresResourceId.Name);
        }
        else
        {
            postgresNode = new ArmResourceNode(
                resourceType: resourceType,
                resourceId: postgresResourceId!,
                subscriptionId: postgresResourceId.SubscriptionId!,
                resourceGroupName: postgresResourceId.ResourceGroupName!,
                resourceName: postgresResourceId.Name);
        }

        var properties = postgresNode.GetNodeProperties();

        // Set authentication type
        properties["authType"] = DetermineAuthType(connectionData);
        properties["source"] = $"{sourceType}:{sourceName}";

        // Add enhanced properties to the knowledge graph
        if (!string.IsNullOrEmpty(connectionData.Database))
            properties["database"] = connectionData.Database;
        if (connectionData.Port != 5432)
            properties["port"] = connectionData.Port.ToString();
        if (!string.IsNullOrEmpty(connectionData.SslMode))
            properties["sslMode"] = connectionData.SslMode;
        if (!string.IsNullOrEmpty(connectionData.DriverFamily))
            properties["driverFamily"] = connectionData.DriverFamily;
        if (!string.IsNullOrEmpty(connectionData.ApplicationName))
            properties["applicationName"] = connectionData.ApplicationName;
        if (connectionData.IsAzureManaged)
        {
            properties["azureManaged"] = "true";
            if (!string.IsNullOrEmpty(connectionData.AzureServerType))
                properties["serverType"] = connectionData.AzureServerType;
        }
        if (connectionData.IsHighAvailability)
        {
            properties["highAvailability"] = "true";
            properties["hostCount"] = connectionData.HostList?.Count.ToString() ?? "unknown";
        }
        properties["connectionFormat"] = connectionData.Format.ToString();
        if (!string.IsNullOrEmpty(connectionData.KeyName))
            properties["sourceKey"] = connectionData.KeyName; await _graphDbClient.AddOrUpdateNodeAsync(postgresNode);

        var edge = new ArmResourceEdge(workloadNode.GetNodeId(), postgresNode.GetNodeId(), Constants.Relationships.PostgreSqlConnected);

        // Add connection-specific properties to the edge
        if (!string.IsNullOrEmpty(connectionData.Database))
            edge.AdditionalProperties.AddOrUpdateEdgeProperty("database", connectionData.Database);
        if (!string.IsNullOrEmpty(connectionData.AuthMethod))
            edge.AdditionalProperties.AddOrUpdateEdgeProperty("authMethod", connectionData.AuthMethod);
        if (!string.IsNullOrEmpty(connectionData.SslMode))
            edge.AdditionalProperties.AddOrUpdateEdgeProperty("sslMode", connectionData.SslMode);

        await _graphDbClient.AddOrUpdateEdgeAsync(edge);

        _logger.LogDebug($"Linked workload {workloadNode.GetNodeId()} with PostgreSQL resource {postgresResourceId}");
        return postgresNode;
    }

    private string DetermineAuthType(PostgreSqlConnectionData connectionData)
    {
        if (connectionData == null) return "unknown";

        // Use the enhanced auth method detection
        if (!string.IsNullOrEmpty(connectionData.AuthMethod))
        {
            return connectionData.AuthMethod;
        }

        // Fallback to original logic for compatibility
        var connectionString = connectionData.OriginalConnectionString;
        if (connectionString.Contains("Authentication=Active Directory Managed Identity", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("Integrated Security=true", StringComparison.OrdinalIgnoreCase))
        {
            return "managedIdentity";
        }
        else if (connectionString.Contains("SSL Mode=", StringComparison.OrdinalIgnoreCase) ||
                 connectionData.SslMode != null)
        {
            return "connectionStringWithSSL";
        }
        return "connectionString";
    }

    private PostgreSqlConnectionData? ParsePostgreSqlConnectionString(string connectionString, string? keyName = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return null;

        var format = DetectFormat(connectionString, keyName);
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            switch (format)
            {
                case PostgreSqlConnectionFormat.URL:
                    parameters = ParseUrlFormat(connectionString);
                    break;
                case PostgreSqlConnectionFormat.SemicolonList:
                    parameters = ParseSemicolonFormat(connectionString);
                    break;
                case PostgreSqlConnectionFormat.KeyValueList:
                    parameters = ParseKeyValueFormat(connectionString);
                    break;
                case PostgreSqlConnectionFormat.JSONPayload:
                    parameters = ParseJsonFormat(connectionString);
                    break;
                case PostgreSqlConnectionFormat.ODBC:
                    parameters = ParseOdbcFormat(connectionString);
                    break;
                case PostgreSqlConnectionFormat.ServiceName:
                    // Service name resolution would require access to service files
                    // For now, just capture the service name
                    parameters["service"] = connectionString.Trim();
                    break;
                default:
                    return null;
            }

            // Normalize parameter names to canonical forms
            var normalized = NormalizeParameters(parameters);

            return CreateConnectionData(normalized, format, connectionString, keyName);
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning($"Failed to parse PostgreSQL connection string: {ex.Message}");
            return null;
        }
    }

    private Dictionary<string, string> ParseUrlFormat(string connectionString)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // Handle special URL schemes
            var uri = connectionString;
            if (uri.StartsWith("postgresql+"))
            {
                // Extract driver variant (e.g., postgresql+psycopg2://)
                var plusIndex = uri.IndexOf('+');
                var schemeEnd = uri.IndexOf("://");
                if (plusIndex > 0 && schemeEnd > plusIndex)
                {
                    parameters["driver_variant"] = uri.Substring(plusIndex + 1, schemeEnd - plusIndex - 1);
                }
                uri = "postgresql" + uri.Substring(uri.IndexOf("://"));
            }
            else if (uri.StartsWith("jdbc:postgresql://"))
            {
                parameters["driver_family"] = "java";
                uri = uri.Substring(5); // Remove "jdbc:"
            }

            var parsedUri = new Uri(uri);

            // Extract host(s) and port(s)
            if (!string.IsNullOrEmpty(parsedUri.Host))
            {
                parameters["host"] = parsedUri.Host;
                if (parsedUri.Port != -1 && parsedUri.Port != 5432)
                {
                    parameters["port"] = parsedUri.Port.ToString();
                }
            }

            // Extract database name
            if (!string.IsNullOrEmpty(parsedUri.LocalPath) && parsedUri.LocalPath != "/")
            {
                parameters["dbname"] = parsedUri.LocalPath.TrimStart('/');
            }

            // Extract user info
            if (!string.IsNullOrEmpty(parsedUri.UserInfo))
            {
                var userInfo = parsedUri.UserInfo.Split(':');
                parameters["user"] = HttpUtility.UrlDecode(userInfo[0]);
                if (userInfo.Length > 1)
                {
                    parameters["password"] = HttpUtility.UrlDecode(userInfo[1]);
                }
            }

            // Extract query parameters
            if (!string.IsNullOrEmpty(parsedUri.Query))
            {
                var queryParams = HttpUtility.ParseQueryString(parsedUri.Query);
                foreach (string key in queryParams.Keys)
                {
                    if (!string.IsNullOrEmpty(key))
                    {
                        parameters[key] = queryParams[key]!;
                    }
                }
            }
        }
        catch (UriFormatException)
        {
            // Fallback to manual parsing for malformed URLs
            var match = Regex.Match(connectionString,
                @"^(?:postgresql|postgres|jdbc:postgresql)(?:\+(?<driver>[^:]+))?://(?:(?<user>[^:@]+)(?::(?<password>[^@]+))?@)?(?<host>[^:/]+)(?::(?<port>\d+))?(?:/(?<database>[^?]+))?(?:\?(?<query>.+))?$",
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                if (match.Groups["driver"].Success)
                    parameters["driver_variant"] = match.Groups["driver"].Value;
                if (match.Groups["user"].Success)
                    parameters["user"] = HttpUtility.UrlDecode(match.Groups["user"].Value);
                if (match.Groups["password"].Success)
                    parameters["password"] = HttpUtility.UrlDecode(match.Groups["password"].Value);
                if (match.Groups["host"].Success)
                    parameters["host"] = match.Groups["host"].Value;
                if (match.Groups["port"].Success)
                    parameters["port"] = match.Groups["port"].Value;
                if (match.Groups["database"].Success)
                    parameters["dbname"] = match.Groups["database"].Value;
                if (match.Groups["query"].Success)
                {
                    var queryString = match.Groups["query"].Value;
                    var queryParams = HttpUtility.ParseQueryString(queryString);
                    foreach (string key in queryParams.Keys)
                    {
                        if (!string.IsNullOrEmpty(key))
                        {
                            parameters[key] = queryParams[key]!;
                        }
                    }
                }
            }
        }

        return parameters;
    }

    private Dictionary<string, string> ParseSemicolonFormat(string connectionString)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var equalIndex = part.IndexOf('=');
            if (equalIndex > 0)
            {
                var key = part.Substring(0, equalIndex).Trim();
                var value = part.Substring(equalIndex + 1).Trim();

                // Handle space-containing keys like "SSL Mode"
                parameters[key] = value;
            }
        }

        return parameters;
    }

    private Dictionary<string, string> ParseKeyValueFormat(string connectionString)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Use regex to handle quoted values and spaces
        var matches = Regex.Matches(connectionString, @"(\w+)=(?:'([^']*)'|""([^""]*)""|((?:[^\s]|(?<=\\)\s)+))");

        foreach (Match match in matches)
        {
            var key = match.Groups[1].Value;
            var value = match.Groups[2].Success ? match.Groups[2].Value :
                       match.Groups[3].Success ? match.Groups[3].Value :
                       match.Groups[4].Value;

            if (value != null)
            {
                parameters[key] = value.Replace("\\ ", " "); // Handle escaped spaces
            }
        }

        return parameters;
    }

    private Dictionary<string, string> ParseJsonFormat(string connectionString)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var document = JsonDocument.Parse(connectionString);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in root.EnumerateObject())
                {
                    var value = property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString(),
                        JsonValueKind.Number => property.Value.GetDecimal().ToString(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        JsonValueKind.Array => string.Join(",", property.Value.EnumerateArray().Select(e => e.GetString())),
                        _ => property.Value.GetRawText()
                    };

                    if (!string.IsNullOrEmpty(value))
                    {
                        parameters[property.Name] = value;
                    }
                }

                // Handle special cases for multi-host JSON
                if (parameters.ContainsKey("hosts") && !parameters.ContainsKey("host"))
                {
                    parameters["host"] = parameters["hosts"];
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogInternalWarning($"Failed to parse JSON connection string: {ex.Message}");
        }

        return parameters;
    }

    private Dictionary<string, string> ParseOdbcFormat(string connectionString)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var equalIndex = part.IndexOf('=');
            if (equalIndex > 0)
            {
                var key = part.Substring(0, equalIndex).Trim();
                var value = part.Substring(equalIndex + 1).Trim();

                // Map ODBC-specific keys to standard PostgreSQL parameters
                var normalizedKey = key.ToLowerInvariant() switch
                {
                    "server" => "host",
                    "uid" => "user",
                    "pwd" => "password",
                    "database" => "dbname",
                    _ => key
                };

                parameters[normalizedKey] = value;
            }
        }

        return parameters;
    }

    private Dictionary<string, string> NormalizeParameters(Dictionary<string, string> parameters)
    {
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Mapping table for parameter name normalization
        var parameterMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["hostaddr"] = "host",
            ["server"] = "host",
            ["hostname"] = "host",
            ["Host"] = "host",
            ["SERVER"] = "host",
            ["database"] = "dbname",
            ["db"] = "dbname",
            ["Database"] = "dbname",
            ["DATABASE"] = "dbname",
            ["initial catalog"] = "dbname",
            ["username"] = "user",
            ["Username"] = "user",
            ["UID"] = "user",
            ["user id"] = "user",
            ["pwd"] = "password",
            ["pass"] = "password",
            ["Password"] = "password",
            ["PWD"] = "password",
            ["Port"] = "port",
            ["PORT"] = "port",
            ["SSL Mode"] = "sslmode",
            ["ssl"] = "sslmode",
            ["SSLMode"] = "sslmode",
            ["GSS Encryption Mode"] = "gssencmode",
            ["Channel Binding"] = "channel_binding",
            ["Target Server Type"] = "target_session_attrs",
            ["targetServerType"] = "target_session_attrs",
            ["ApplicationName"] = "application_name",
            ["Application Name"] = "application_name",
            ["Connection Timeout"] = "connect_timeout",
            ["Timeout"] = "connect_timeout",
            ["Command Timeout"] = "command_timeout",
            ["Pooling"] = "pooling",
            ["Min Pool Size"] = "min_pool_size",
            ["MinPoolSize"] = "min_pool_size",
            ["Max Pool Size"] = "max_pool_size",
            ["MaxPoolSize"] = "max_pool_size",
            ["TCP Keepalives Enabled"] = "keepalives",
            ["keepAlive"] = "keepalives",
            ["TCP Keepalives Idle"] = "keepalives_idle",
            ["TCP Keepalives Interval"] = "keepalives_interval",
            ["TCP Keepalives Count"] = "keepalives_count",
            ["Load Balance Hosts"] = "load_balance_hosts",
            ["loadBalanceHosts"] = "load_balance_hosts",
            ["Authentication"] = "authentication",
            ["Encrypt"] = "encrypt",
            ["Trust Server Certificate"] = "trust_server_certificate"
        };

        foreach (var kvp in parameters)
        {
            var canonicalKey = parameterMappings.TryGetValue(kvp.Key, out var mapped) ? mapped : kvp.Key.ToLowerInvariant();
            normalized[canonicalKey] = kvp.Value;
        }

        return normalized;
    }

    private PostgreSqlConnectionData? CreateConnectionData(Dictionary<string, string> parameters,
        PostgreSqlConnectionFormat format, string originalConnectionString, string? keyName)
    {
        var data = new PostgreSqlConnectionData
        {
            OriginalConnectionString = originalConnectionString,
            Format = format,
            KeyName = keyName,
            Parameters = parameters
        };

        // Extract basic connection properties
        if (parameters.TryGetValue("host", out var host))
            data.Host = host;
        if (parameters.TryGetValue("dbname", out var database))
            data.Database = database;
        if (parameters.TryGetValue("user", out var username))
            data.Username = username;

        if (parameters.TryGetValue("port", out var portStr) && int.TryParse(portStr, out var port))
        {
            data.Port = port;
        }

        // Extract security properties
        if (parameters.TryGetValue("sslmode", out var sslMode))
            data.SslMode = sslMode;
        if (parameters.TryGetValue("authentication", out var authType))
            data.AuthenticationType = authType;
        if (parameters.TryGetValue("application_name", out var appName))
            data.ApplicationName = appName;

        // Detect driver family
        data.DriverFamily = DetectDriverFamily(originalConnectionString, keyName, parameters);

        // Detect authentication method
        data.AuthMethod = DetermineAuthMethod(parameters, originalConnectionString);

        // Detect Azure specifics
        data.IsAzureManaged = data.Host?.Contains(".postgres.database.azure.com", StringComparison.OrdinalIgnoreCase) == true;
        if (data.IsAzureManaged && data.Host != null)
        {
            data.AzureServerType = DetermineAzureServerType(data.Host);
        }

        // Extract high availability info
        if (data.Host?.Contains(',') == true)
        {
            data.HostList = data.Host.Split(',').Select(h => h.Trim()).ToList();
            data.IsHighAvailability = true;
        }

        return data.Host != null ? data : null;
    }

    private string DetectDriverFamily(string connectionString, string? keyName, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrEmpty(connectionString)) return "unknown";

        // URL scheme patterns
        if (connectionString.StartsWith("jdbc:postgresql://")) return "java";
        if (connectionString.StartsWith("postgresql+psycopg2://")) return "python-psycopg2";
        if (connectionString.StartsWith("postgresql+asyncpg://")) return "python-asyncpg";
        if (connectionString.StartsWith("postgresql+psycopg://")) return "python-psycopg3";
        if (connectionString.StartsWith("pgsql://")) return "php";

        // Format-based detection
        if (connectionString.Contains("SSL Mode=") || connectionString.Contains("Pooling=")) return "dotnet";
        if (connectionString.Contains("DRIVER={PostgreSQL")) return "odbc";

        // Key name patterns
        if (keyName != null)
        {
            if (keyName.StartsWith("SPRING_DATASOURCE_")) return "java-spring";
            if (keyName.Contains("RAILS_")) return "ruby-rails";
            if (keyName.StartsWith("DB_") && !keyName.Contains("URL")) return "generic-split";
        }

        // JSON structure patterns
        if (connectionString.TrimStart().StartsWith("{"))
        {
            if (connectionString.Contains("\"ssl\"") && connectionString.Contains("\"database\"")) return "nodejs";
            if (connectionString.Contains("\"sslmode\"")) return "python";
        }

        // Driver variant from parameters
        if (parameters.TryGetValue("driver_variant", out var variant))
        {
            return $"python-{variant}";
        }

        return "standard";
    }

    private string DetermineAuthMethod(Dictionary<string, string> parameters, string connectionString)
    {
        // Check for managed identity patterns
        if (parameters.TryGetValue("authentication", out var authType))
        {
            if (authType.Contains("Active Directory", StringComparison.OrdinalIgnoreCase) ||
                authType.Contains("Managed Identity", StringComparison.OrdinalIgnoreCase))
            {
                return "managedIdentity";
            }
        }

        // Check for certificate authentication
        if (parameters.ContainsKey("sslcert") && parameters.ContainsKey("sslkey"))
        {
            return "certificate";
        }

        // Check for GSS/Kerberos
        if (parameters.TryGetValue("gssencmode", out var gssMode) &&
            gssMode != "disable")
        {
            return "gss";
        }

        // Check connection string patterns
        if (connectionString.Contains("Authentication=Active Directory", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("Integrated Security=true", StringComparison.OrdinalIgnoreCase))
        {
            return "managedIdentity";
        }

        // Check for password presence
        if (parameters.ContainsKey("password"))
        {
            return "password";
        }

        return "unknown";
    }

    private string DetermineAzureServerType(string host)
    {
        if (string.IsNullOrEmpty(host)) return "unknown";

        // This is a simplified heuristic - in practice you might need to query Azure APIs
        // to determine the exact server type
        if (host.Contains("hyperscale") || host.Contains("citus"))
        {
            return "hyperscale";
        }

        // Most new Azure PostgreSQL instances are flexible servers
        return "flexible";
    }

    public async Task<ArmResourceNode?> TryLinkPostgreSqlResourceById(GraphNode workloadNode, string possiblePostgreSqlResource, string sourceType, string sourceName)
    {
        try
        {
            var postgresId = new ResourceIdentifier(possiblePostgreSqlResource);
            if (postgresId is null)
            {
                _logger.LogInternalWarning($"Invalid PostgreSQL resource ID: {possiblePostgreSqlResource}");
                return null;
            }

            var resourceType = postgresId.ResourceType.ToString();
            // Validate it's a PostgreSQL resource
            if (!resourceType.Contains("Microsoft.DBforPostgreSQL", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Create PostgreSqlFlexServerNode for flexible servers, ArmResourceNode for others
            ArmResourceNode postgresNode;
            if (resourceType == "Microsoft.DBforPostgreSQL/flexibleServers")
            {
                postgresNode = new PostgreSqlFlexServerNode(
                    resourceType: resourceType,
                    resourceId: postgresId!,
                    subscriptionId: postgresId.SubscriptionId!,
                    resourceGroupName: postgresId.ResourceGroupName!,
                    resourceName: postgresId.Name);
            }
            else
            {
                postgresNode = new ArmResourceNode(
                    resourceType: resourceType,
                    resourceId: postgresId!,
                    subscriptionId: postgresId.SubscriptionId!,
                    resourceGroupName: postgresId.ResourceGroupName!,
                    resourceName: postgresId.Name);
            }

            var properties = postgresNode.GetNodeProperties();
            properties["source"] = $"{sourceType}:{sourceName}";
            properties["authType"] = "resourceId";

            await _graphDbClient.AddOrUpdateNodeAsync(postgresNode);

            var edge = new ArmResourceEdge(workloadNode.GetNodeId(), postgresNode.GetNodeId(), Constants.Relationships.PostgreSqlConnected);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);

            _logger.LogDebug($"Linked workload {workloadNode.GetNodeId()} with PostgreSQL resource {postgresId}");
            return postgresNode;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"Error linking PostgreSQL resource from value: {possiblePostgreSqlResource}. Exception: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// New overload to handle multiple environment variables for Individual Variables pattern
    /// </summary>
    public async Task<ArmResourceNode?> GetPostgreSqlResourceFromEnvironmentVariablesAsync(
        GraphNode workloadNode,
        Dictionary<string, string> environmentVariables,
        string sourceType)
    {
        try
        {
            var connectionData = BuildConnectionFromEnvironmentVariables(environmentVariables);
            if (connectionData == null || connectionData.Host == null) return null;

            var rawHost = connectionData.Host;
            var database = connectionData.Database;
            var serverName = rawHost;
            int portIndex = serverName.IndexOf(":");
            if (portIndex > 0)
            {
                serverName = serverName.Substring(0, portIndex);
            }

            var serverBaseName = serverName;
            if (serverBaseName.EndsWith(azurePostgreSqlSuffix, StringComparison.OrdinalIgnoreCase))
            {
                serverBaseName = serverBaseName.Substring(0, serverBaseName.Length - azurePostgreSqlSuffix.Length);
            }

            _logger.LogDebug($"Parsed PostgreSQL server from environment variables - Host: {serverName}, Database: {database}");

            var subscription = _armClient.GetSubscriptionResource(new ResourceIdentifier("/subscriptions/" + workloadNode.GetSubscriptionId()));

            // Check for flexible servers first
            await foreach (var server in subscription.GetGenericResourcesAsync(filter: $"resourceType eq 'Microsoft.DBforPostgreSQL/flexibleServers' and name eq '{serverBaseName.ToLowerInvariant()}'"))
            {
                return await CreatePostgreSqlNode(workloadNode, server, connectionData, sourceType, "environment_variables", "Microsoft.DBforPostgreSQL/flexibleServers");
            }

            // Check for single servers
            await foreach (var server in subscription.GetGenericResourcesAsync(filter: $"resourceType eq 'Microsoft.DBforPostgreSQL/servers' and name eq '{serverBaseName.ToLowerInvariant()}'"))
            {
                return await CreatePostgreSqlNode(workloadNode, server, connectionData, sourceType, "environment_variables", "Microsoft.DBforPostgreSQL/servers");
            }

            // Check for Cosmos DB for PostgreSQL (formerly Hyperscale)
            await foreach (var server in subscription.GetGenericResourcesAsync(filter: $"resourceType eq 'Microsoft.DBforPostgreSQL/serverGroupsv2' and name eq '{serverBaseName.ToLowerInvariant()}'"))
            {
                return await CreatePostgreSqlNode(workloadNode, server, connectionData, sourceType, "environment_variables", "Microsoft.DBforPostgreSQL/serverGroupsv2");
            }

            _logger.LogInternalWarning($"PostgreSQL server with name {serverName} was not found in the subscription.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"Error processing PostgreSQL environment variables: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Detects PostgreSQL connections from individual environment variables
    /// </summary>
    public bool HasPostgreSqlEnvironmentVariables(Dictionary<string, string> environmentVariables)
    {
        if (environmentVariables == null || !environmentVariables.Any()) return false;

        // Check for core PostgreSQL environment variables
        var pgVars = new[] { "PGHOST", "PGDATABASE", "PGUSER", "PGPASSWORD", "PGPORT", "PGSERVICE" };
        var hasAnyPgVar = pgVars.Any(var => environmentVariables.ContainsKey(var) && !string.IsNullOrEmpty(environmentVariables[var]));

        if (hasAnyPgVar) return true;

        // Check for common PostgreSQL connection string variables
        var connectionStringVars = new[] {
            "DATABASE_URL", "POSTGRES_URL", "POSTGRESQL_URL", "POSTGRES_CONNECTION_STRING",
            "DB_CONNECTION_STRING", "POSTGRESQL_CONNECTION_STRING", "DB_URL"
        };

        return connectionStringVars.Any(var =>
            environmentVariables.ContainsKey(var) &&
            !string.IsNullOrEmpty(environmentVariables[var]) &&
            IsPostgreSqlConnectionString(environmentVariables[var], var));
    }

    /// <summary>
    /// Builds connection data from individual environment variables with proper precedence
    /// </summary>
    private PostgreSqlConnectionData? BuildConnectionFromEnvironmentVariables(Dictionary<string, string> environmentVariables)
    {
        // Step 1: Check for direct connection string variables first (highest precedence)
        var connectionStringVars = new[] {
            "DATABASE_URL", "POSTGRES_URL", "POSTGRESQL_URL", "POSTGRES_CONNECTION_STRING",
            "DB_CONNECTION_STRING", "POSTGRESQL_CONNECTION_STRING", "DB_URL"
        }; foreach (var varName in connectionStringVars)
        {
            if (environmentVariables.TryGetValue(varName, out var connectionString) &&
                !string.IsNullOrEmpty(connectionString) &&
                IsPostgreSqlConnectionString(connectionString, varName))
            {
                var connData = ParsePostgreSqlConnectionString(connectionString, varName);
                if (connData != null)
                {
                    connData.SourceVariables = new List<string> { varName };
                    return connData;
                }
            }
        }

        // Step 2: Check for service file reference
        if (environmentVariables.TryGetValue("PGSERVICE", out var serviceName) && !string.IsNullOrEmpty(serviceName))
        {
            var serviceData = new PostgreSqlConnectionData
            {
                Service = serviceName,
                Format = PostgreSqlConnectionFormat.ServiceName,
                OriginalConnectionString = serviceName,
                SourceVariables = new List<string> { "PGSERVICE" }
            };

            // Service resolution would need access to service files, which we can't do in this architecture
            // For now, just note that it's a service reference
            return serviceData;
        }

        // Step 3: Build from individual PG* environment variables (Individual Variables format)
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sourceVars = new List<string>();

        var pgVarMap = new Dictionary<string, string>
        {
            ["PGHOST"] = "host",
            ["PGPORT"] = "port",
            ["PGDATABASE"] = "dbname",
            ["PGUSER"] = "user",
            ["PGPASSWORD"] = "password",
            ["PGSSLMODE"] = "sslmode",
            ["PGGSSENCMODE"] = "gssencmode",
            ["PGCHANNELBINDING"] = "channel_binding",
            ["PGTARGETSESSIONATTRS"] = "target_session_attrs",
            ["PGAPPNAME"] = "application_name",
            ["PGCONNECT_TIMEOUT"] = "connect_timeout"
        };

        foreach (var kvp in pgVarMap)
        {
            if (environmentVariables.TryGetValue(kvp.Key, out var value) && !string.IsNullOrEmpty(value))
            {
                parameters[kvp.Value] = value;
                sourceVars.Add(kvp.Key);
            }
        }

        // Step 4: Check for framework-specific variables
        var frameworkVars = new Dictionary<string, string>
        {
            ["SPRING_DATASOURCE_URL"] = "spring_url",
            ["DB_HOST"] = "host",
            ["DB_PORT"] = "port",
            ["DB_NAME"] = "dbname",
            ["DB_USER"] = "user",
            ["DB_PASSWORD"] = "password",
            ["POSTGRES_HOST"] = "host",
            ["POSTGRES_PORT"] = "port",
            ["POSTGRES_DB"] = "dbname",
            ["POSTGRES_USER"] = "user",
            ["POSTGRES_PASSWORD"] = "password"
        };

        foreach (var kvp in frameworkVars)
        {
            if (environmentVariables.TryGetValue(kvp.Key, out var value) && !string.IsNullOrEmpty(value))
            {
                if (kvp.Value == "spring_url" && IsPostgreSqlConnectionString(value, kvp.Key))
                {
                    // Handle Spring URL separately
                    var springData = ParsePostgreSqlConnectionString(value, kvp.Key);
                    if (springData != null)
                    {
                        springData.SourceVariables = new List<string> { kvp.Key };
                        return springData;
                    }
                }
                else if (!parameters.ContainsKey(kvp.Value))
                {
                    parameters[kvp.Value] = value;
                    sourceVars.Add(kvp.Key);
                }
            }
        }

        // Must have at least host or database to be considered valid
        if (!parameters.ContainsKey("host") && !parameters.ContainsKey("dbname"))
        {
            return null;
        }

        // Normalize and create connection data
        var normalized = NormalizeParameters(parameters);
        var connectionData = CreateConnectionData(normalized, PostgreSqlConnectionFormat.IndividualVars,
            string.Join(", ", sourceVars), null);
        if (connectionData == null)
        {
            return null;
        }

        connectionData.SourceVariables = sourceVars;

        return connectionData;
    }

    private class PostgreSqlConnectionData
    {
        public string? Host { get; set; }
        public string? Database { get; set; }
        public int Port { get; set; } = 5432;
        public string? Username { get; set; }
        public string? SslMode { get; set; }
        public string? AuthenticationType { get; set; }
        public string? ApplicationName { get; set; }
        public string? DriverFamily { get; set; }
        public string? AuthMethod { get; set; }
        public bool IsAzureManaged { get; set; }
        public string? AzureServerType { get; set; }
        public bool IsHighAvailability { get; set; }
        public List<string>? HostList { get; set; }
        public PostgreSqlConnectionFormat Format { get; set; }
        public required string OriginalConnectionString { get; set; }
        public string? KeyName { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();

        // New properties for Individual Variables support
        public string? Service { get; set; }
        public List<string>? SourceVariables { get; set; }
    }
}
