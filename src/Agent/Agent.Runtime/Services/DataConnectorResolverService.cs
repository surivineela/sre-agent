// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Framework;
using Agent.Framework.Reasoning.Models;
using Agent.Plugins.Tools;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agent.Runtime.Services;

public class DataConnectorResolverService : IConnectorResolver
{
    private readonly IOptionsMonitor<List<DataConnectorInstanceSettings>> _connectorSettings;
    private readonly ILogger<DataConnectorResolverService> _logger;
    private readonly IHostEnvironment _env;

    public DataConnectorResolverService(
        IOptionsMonitor<List<DataConnectorInstanceSettings>> connectorSettings,
        ILogger<DataConnectorResolverService> logger, IHostEnvironment env)
    {
        _connectorSettings = connectorSettings;
        _logger = logger;
        _env = env;
    }

    /// <summary>
    /// Retrieves a connector definition instance from configuration settings based on the provided
    /// <paramref name="connectorName"/>, <paramref name="connectorType"/>, and <paramref name="dataSource"/>.
    ///
    /// This method is designed to support agents, tools, and connectors where each tool references
    /// a connector in its <c>Connector</c> property. Since tools may be authored in different contexts,
    /// this resolution mechanism supports three scenarios:
    ///
    /// 1. <b>By Name</b> (backward compatibility):
    ///    If <paramref name="connectorName"/> is specified, the method looks up a connector instance
    ///    with a matching name in the current configuration.
    ///
    /// 2. <b>By Type</b> (design-time abstraction):
    ///    If <paramref name="connectorName"/> is not provided but <paramref name="connectorType"/> is,
    ///    the method retrieves the first available connector of the requested type.
    ///    This allows tool authors who do not know the concrete cluster URL at design time
    ///    to still bind to a connector based on its type (e.g., "Kusto").
    ///
    /// 3. <b>By Runtime DataSource</b> (runtime explicit override):
    ///    If the agent or tool provides a specific <paramref name="dataSource"/> (e.g., a Kusto cluster URL),
    ///    the method selects the connector instance whose configured <c>DataSource</c> matches that host.
    ///    This enables runtime flexibility when the exact target cluster or server is known only at execution.
    ///
    /// Once a matching connector instance is found, this method:
    /// - Creates a strongly-typed connector object (<typeparamref name="T"/>) derived from <see cref="DataConnectorDefinitionBase"/>.
    /// - Populates its core properties (Name, Type, Enabled).
    /// - Assigns authentication settings, defaulting to <c>User</c> auth in development and
    ///   <c>UAMI</c> (User Assigned Managed Identity) in non-development environments.
    /// - If the connector is of type <see cref="KustoConnector"/>, parses the configured <c>DataSource</c>
    ///   to extract the <c>ClusterUrl</c> and <c>Database</c>.
    /// - Validates the connector before returning it.
    ///
    /// Exceptions are thrown when:
    /// - No connectors are configured.
    /// - No matching connector is found by name, type, or data source.
    /// - The connector cannot be created or validated.
    ///
    /// All failures are logged with internal warnings before rethrowing.
    /// </summary>
    /// <typeparam name="T">
    /// The expected connector type, derived from <see cref="DataConnectorDefinitionBase"/>.
    /// For example, <see cref="KustoConnector"/>.
    /// </typeparam>
    /// <param name="connectorName">
    /// Optional explicit connector name. If provided, name-based resolution is attempted first.
    /// </param>
    /// <param name="connectorType">
    /// Connector type identifier (e.g., "Kusto"). Used when name is not provided or not found.
    /// </param>
    /// <param name="dataSource">
    /// Optional runtime data source (e.g., a cluster host prefix).
    /// If provided, it refines type-based lookup to select the connector with a matching data source.
    /// </param>
    /// <returns>
    /// A validated connector instance of type <typeparamref name="T"/> configured for use.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no connectors are available, if a matching connector cannot be found,
    /// or if instantiation/validation fails.
    /// </exception>

    public T GetConnectorFromSettings<T>(string connectorName, string connectorType, string dataSource)
    where T : DataConnectorDefinitionBase, new()
    {
        try
        {
            var connectorSettings = _connectorSettings.CurrentValue;
            if (connectorSettings is null || !connectorSettings.Any())
                throw new InvalidOperationException("No connector settings found.");

            DataConnectorInstanceSettings? settings = null;

            // 1. Resolve by Name (backward compatibility)
            if (!string.IsNullOrWhiteSpace(connectorName))
            {
                settings = connectorSettings.FirstOrDefault(c =>
                    string.Equals(c.Name, connectorName, StringComparison.OrdinalIgnoreCase));
            }

            // 2. Resolve by Type (if not found by name)
            if (settings == null && !string.IsNullOrWhiteSpace(connectorType))
            {
                var candidates = connectorSettings
                    .Where(c => string.Equals(c.DataConnectorType, connectorType, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (!candidates.Any())
                    throw new InvalidOperationException($"No connectors of type '{connectorType}' found.");

                // 3. Refine by DataSource if provided
                if (!string.IsNullOrWhiteSpace(dataSource))
                {
                    foreach (var candidate in candidates)
                    {
                        if (Uri.TryCreate(candidate.DataSource, UriKind.Absolute, out var uri) && uri != null)
                        {
                            if (uri.Host.Contains(dataSource, StringComparison.OrdinalIgnoreCase) ||
                                uri.AbsolutePath.Contains(dataSource, StringComparison.OrdinalIgnoreCase))
                            {
                                settings = candidate;
                                break;
                            }
                        }
                    }
                    if (settings == null)
                    {
                        if (_env.IsDevelopment())
                        {
                            var originalSettings = candidates.First();
                            settings = new DataConnectorInstanceSettings
                            {
                                Name = originalSettings.Name,
                                DataConnectorType = originalSettings.DataConnectorType,
                                DataSource = $"https://{dataSource}.kusto.windows.net/capps", // Override with runtime data source
                                Identity = originalSettings.Identity
                            };
                        }
                        else
                        {
                            throw new InvalidOperationException(
                                $"Connector of type '{connectorType}' with DataSource '{dataSource}' not found.");
                        }
                    }
                }
                else
                {
                    settings = candidates.First(); // Default to first available of that type
                }
            }

            if (settings == null)
            {
                throw new InvalidOperationException(
                    $"Connector could not be resolved for Name='{connectorName}', Type='{connectorType}', DataSource='{dataSource}'.");
            }

            // Build connector instance
            var connector = new T
            {
                Name = settings.Name,
                Type = settings.DataConnectorType,
                Enabled = true, // Configured = enabled by default
                Auth = new ConnectorAuthSettings
                {
                    AuthenticationType = settings.Source == DataConnectorSource.AgentSpace
                        ? ConnectorAuthType.AgentSpace
                        : (_env.IsDevelopment()
                            ? ConnectorAuthType.User
                            : ConnectorAuthType.UAMI),
                    ManagedIdentityResourceId = settings.Identity
                }
            };

            // Special handling for Kusto connectors
            if (connector is KustoConnector kustoConnector &&
                Uri.TryCreate(settings.DataSource, UriKind.Absolute, out var dsUri))
            {
                kustoConnector.ClusterUrl = $"https://{dsUri.Host}";
                kustoConnector.Database = dsUri.AbsolutePath.TrimStart('/');
            }

            connector.Validate();
            return connector;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(
                ex,
                $"Failed to load connector. Name='{connectorName}', Type='{connectorType}', DataSource='{dataSource}'",
                connectorType);

            throw new InvalidOperationException(
                $"Failed to load connector. Name='{connectorName}', Type='{connectorType}', DataSource='{dataSource}'.",
                ex);
        }
    }

    /// <summary>
    /// Gets agent space managed identity for specific platform
    /// </summary>
    /// <param name="platform">Platform type (e.g., "ICM")</param>
    /// <returns>Managed identity resource ID if found, otherwise null</returns>
    public string? GetAgentSpaceDataConnectorIdentity()
    {
        try
        {
            var connectorSettings = _connectorSettings.CurrentValue;
            if (connectorSettings == null || !connectorSettings.Any())
            {
                return null;
            }

            // Find AgentSpace connector matching the platform
            var agentSpaceConnector = connectorSettings.FirstOrDefault(settings =>
                settings.Source == DataConnectorSource.AgentSpace &&
                settings.DataConnectorType.Equals("icm", StringComparison.OrdinalIgnoreCase) == true);
            return agentSpaceConnector?.Identity;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to get AgentSpace identity for platform {Platform}", "icm");
            return null;
        }
    }

    /// <summary>
    /// Gets a list of all configured data connectors with basic information
    /// </summary>
    /// <returns>List of data connector basic information</returns>
    public List<DataConnectorBasicInfo> GetAllDataConnectors()
    {
        try
        {
            var connectorSettings = _connectorSettings.CurrentValue;
            if (connectorSettings == null || !connectorSettings.Any())
            {
                return new List<DataConnectorBasicInfo>();
            }

            return connectorSettings.Select(settings => new DataConnectorBasicInfo
            {
                Name = settings.Name,
                ConnectorType = settings.DataConnectorType,
                DataSource = settings.DataSource,
                Identity = settings.Identity,
                Source = settings.Source.ToString()
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to get list of data connectors");
            return new List<DataConnectorBasicInfo>();
        }
    }
}
