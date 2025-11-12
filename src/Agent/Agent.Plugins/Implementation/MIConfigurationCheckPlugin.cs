// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Logging;
using Agent.Plugins.Interface;
using Azure;
using Azure.Core;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation
{
    public class MIConfigurationCheckPlugin : IMIConfigurationCheckPlugin
    {
        private readonly ILogger<MIConfigurationCheckPlugin> _logger;
        private readonly IArmClientFactory _armClientFactory;

        public MIConfigurationCheckPlugin(ILogger<MIConfigurationCheckPlugin> logger, IArmClientFactory armClientFactory)
        {
            _armClientFactory = armClientFactory;
            _logger = logger;
        }

        public async Task<SqlConnectionDescriptor> CheckSqlConnectionTypeAsync(string resourceId)
        {
            _logger.LogInternalInformation($"[check_sql_connection_type] Invoked with resourceId: {resourceId}");

            var armClient = await _armClientFactory.GetArmOperationClient();

            var armResourceId = new ResourceIdentifier(resourceId);
            var groupid = ResourceGroupResource.CreateResourceIdentifier(armResourceId.SubscriptionId, armResourceId.ResourceGroupName);

            try
            {
                var group = armClient.GetResourceGroupResource(groupid);
                var siteResponse = await group.GetWebSiteAsync(armResourceId.Name);

                var appSettingsResponse = await siteResponse.Value.GetApplicationSettingsAsync();
                var appSettings = appSettingsResponse.Value.Properties;
                foreach (var setting in appSettings)
                {
                    if (setting.Key.Contains("sql", StringComparison.OrdinalIgnoreCase))
                    {
                        if (setting.Value.Contains("Password"))
                        {
                            var (serverAddress, serverName, dbName) = GetConnectionDetails(setting.Value);
                            return new SqlConnectionDescriptor(
                                SqlServerAddress: serverAddress,
                                SqlServerResourceId: $"/subscriptions/{armResourceId.SubscriptionId}/resourceGroups/{armResourceId.ResourceGroupName}/providers/Microsoft.Sql/servers/{serverName}",
                                DatabaseName: dbName,
                                ConnectionType: ConnectionType.Password);
                        }
                        else
                        {
                            var (serverAddress, serverName, dbName) = GetConnectionDetails(setting.Value);
                            return new SqlConnectionDescriptor(
                                SqlServerAddress: setting.Value,
                                SqlServerResourceId: $"/subscriptions/{armResourceId.SubscriptionId}/resourceGroups/{armResourceId.ResourceGroupName}/providers/Microsoft.Sql/servers/{serverName}",
                                DatabaseName: dbName,
                                ConnectionType: ConnectionType.ManagedIdentity);
                        }
                    }
                }

                return new SqlConnectionDescriptor(
                    SqlServerAddress: "None",
                    SqlServerResourceId: "None",
                    DatabaseName: "None",
                    ConnectionType: ConnectionType.None);
            }
            catch (RequestFailedException ex)
            {
                _logger.LogInternalError($"Error in CheckSqlConnectionTypeAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<string> CheckSqlResourceIdForAppAsync(string resourceId)
        {
            _logger.LogInternalInformation($"[check_sql_resource_id] Invoked with resourceId: {resourceId}");

            var armClient = await _armClientFactory.GetArmOperationClient();

            var armResourceId = new ResourceIdentifier(resourceId);
            var groupid = ResourceGroupResource.CreateResourceIdentifier(armResourceId.SubscriptionId, armResourceId.ResourceGroupName);

            try
            {
                var group = armClient.GetResourceGroupResource(groupid);
                var siteResponse = await group.GetWebSiteAsync(armResourceId.Name);

                var appSettingsResponse = await siteResponse.Value.GetApplicationSettingsAsync();
                var appSettings = appSettingsResponse.Value.Properties;
                foreach (var setting in appSettings)
                {
                    if (setting.Key.Contains("sql", StringComparison.OrdinalIgnoreCase))
                    {
                        if (setting.Value.Contains("Password"))
                        {
                            var (_, serverName, _) = GetConnectionDetails(setting.Value);
                            return $"/subscriptions/{armResourceId.SubscriptionId}/resourceGroups/{armResourceId.ResourceGroupName}/providers/Microsoft.Sql/servers/{serverName}";
                        }
                        else
                        {
                            var (_, serverName, _) = GetConnectionDetails(setting.Value);
                            return $"/subscriptions/{armResourceId.SubscriptionId}/resourceGroups/{armResourceId.ResourceGroupName}/providers/Microsoft.Sql/servers/{serverName}";
                        }
                    }
                }

                return "No sql server exists";
            }
            catch (RequestFailedException ex)
            {
                _logger.LogInternalError($"Error in CheckSqlResourceIdForAppAsync: {ex.Message}");
                throw;
            }
        }

        private (string serverAddress, string serverName, string databaseName) GetConnectionDetails(string connectionString)
        {
            string serverAddress = string.Empty;
            string serverName = string.Empty;
            string databaseName = string.Empty;

            var parameters = connectionString.Split(';');

            foreach (var parameter in parameters)
            {
                if (parameter.StartsWith("Server=", StringComparison.OrdinalIgnoreCase))
                {
                    serverAddress = parameter.Substring("Server=".Length);
                    if (serverAddress.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
                    {
                        serverAddress = serverAddress.Substring("tcp:".Length);
                    }
                    serverName = ExtractServerName(serverAddress);
                }
                else if (parameter.StartsWith("Initial Catalog=", StringComparison.OrdinalIgnoreCase) ||
                         parameter.StartsWith("Database=", StringComparison.OrdinalIgnoreCase))
                {
                    databaseName = parameter.Split('=')[1];
                }
            }

            return (serverAddress, serverName, databaseName);
        }

        private string ExtractServerName(string serverAddress)
        {
            if (serverAddress.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
            {
                serverAddress = serverAddress.Substring("tcp:".Length);
            }

            int firstDotIndex = serverAddress.IndexOf('.');
            if (firstDotIndex != -1)
            {
                serverAddress = serverAddress.Substring(0, firstDotIndex);
            }

            int portIndex = serverAddress.IndexOf(',');
            if (portIndex != -1)
            {
                serverAddress = serverAddress.Substring(0, portIndex);
            }

            return serverAddress;
        }
    }
}

