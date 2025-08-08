// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Framework;
using Agent.Framework.Reasoning.Models;
using Agent.Plugins.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agent.Runtime.Services;

public class DataConnectorResolverService : IConnectorResolver
{
    private readonly IOptionsMonitor<List<DataConnectorInstanceSettings>> _connectorSettings;
    private readonly ILogger<DataConnectorResolverService> _logger;

    public DataConnectorResolverService(
        IOptionsMonitor<List<DataConnectorInstanceSettings>> connectorSettings,
        ILogger<DataConnectorResolverService> logger)
    {
        _connectorSettings = connectorSettings;
        _logger = logger;
    }

    public T GetConnectorFromSettings<T>(string connectorName) where T : DataConnectorDefinitionBase, new()
    {
        try
        {
            var connectorSettings = _connectorSettings.CurrentValue;
            var settings = connectorSettings?.FirstOrDefault(c =>
                string.Equals(c.Name, connectorName, StringComparison.OrdinalIgnoreCase));

            // TODO: Filter by DataConnectorType matching with T

            if (settings == null)
            {
                throw new InvalidOperationException($"Connector '{connectorName}' not found in settings.");
            }

            // Create instance of the specific connector type
            var connector = new T();

            // Set base properties from DataConnectorDefinitionBase
            connector.Name = settings.Name;
            connector.Type = settings.DataConnectorType;
            connector.Enabled = true; // Default to enabled since it's in config

            // Set auth settings
            connector.Auth = new ConnectorAuthSettings
            {
                AuthenticationType = ConnectorAuthType.UAMI,
                ManagedIdentityResourceId = settings.Identity
            };

            // Handle specific connector types
            if (connector is KustoConnector kustoConnector &&
                !string.IsNullOrWhiteSpace(settings.DataSource))
            {
                // Parse DataSource for Kusto specific properties
                var dataSourceUri = new Uri(settings.DataSource);
                kustoConnector.ClusterUrl = $"https://{dataSourceUri.Host}";
                kustoConnector.Database = dataSourceUri.AbsolutePath.TrimStart('/');
            }

            connector.Validate();
            return connector;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to load connector '{ConnectorName}' from settings", connectorName);
            throw new InvalidOperationException($"Failed to load connector '{connectorName}' from settings.", ex);
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
                Identity = settings.Identity
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to get list of data connectors");
            return new List<DataConnectorBasicInfo>();
        }
    }
}
