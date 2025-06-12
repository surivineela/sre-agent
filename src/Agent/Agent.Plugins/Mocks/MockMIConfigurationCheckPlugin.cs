// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Mocks
{
    public class MockMIConfigurationCheckPlugin : IMIConfigurationCheckPlugin
    {
        private readonly HashSet<string> _processedApps = new();
        private const string BaseSubscriptionId = "29e3378b-0aaf-45da-b3c6-6fd0eea164e4";
        private const string BaseResourceGroup = "my-resource-group";

        public void MarkAppAsProcessed(string resourceId)
        {
            _processedApps.Add(resourceId);
        }

        private (string serverName, string serverId, string database) GetSqlServerInfo(string appResourceId)
        {
            // Extract app name from resource ID
            var appName = appResourceId.Split('/').Last();
            
            // Generate unique SQL server info for each app
            return appName switch
            {
                "app1" => (
                    "sql-server-1.data.sql.net",
                    $"/subscriptions/{BaseSubscriptionId}/resourceGroups/{BaseResourceGroup}/providers/Microsoft.Sql/servers/sql-server-1",
                    "productdb-1"
                ),
                "app2" => (
                    "sql-server-2.data.sql.net",
                    $"/subscriptions/{BaseSubscriptionId}/resourceGroups/{BaseResourceGroup}/providers/Microsoft.Sql/servers/sql-server-2",
                    "productdb-2"
                ),
                _ => (
                    "default-sql-server.data.sql.net",
                    $"/subscriptions/{BaseSubscriptionId}/resourceGroups/{BaseResourceGroup}/providers/Microsoft.Sql/servers/default-sql-server",
                    "defaultdb"
                )
            };
        }

        public Task<SqlConnectionDescriptor> CheckSqlConnectionTypeAsync(string resourceId)
        {
            var (serverName, serverId, database) = GetSqlServerInfo(resourceId);
            
            if (_processedApps.Contains(resourceId))
            {
                return Task.FromResult(new SqlConnectionDescriptor(
                    serverName,
                    serverId,
                    database,
                    ConnectionType.ManagedIdentity));
            }

            return Task.FromResult(new SqlConnectionDescriptor(
                serverName,
                serverId,
                database,
                ConnectionType.Password));
        }

        public Task<string> CheckSqlResourceIdForAppAsync(string resourceId)
        {
            var (_, serverId, _) = GetSqlServerInfo(resourceId);
            return Task.FromResult(serverId);
        }

        public Task<ManagedIdentityInfo> GetManagedIdentityInfo(string resourceId)
        {
            if (_processedApps.Contains(resourceId))
            {
                return Task.FromResult(new ManagedIdentityInfo
                {
                    IsConnected = true,
                    RepoUrl = "https://github.com/testorg/testrepo",
                    Branch = "main",
                    WorkflowPath = ".github/workflows/managed-identity.yml",
                    Details = "Managed Identity"
                });
            }

            return Task.FromResult(new ManagedIdentityInfo
            {
                IsConnected = false,
                RepoUrl = "",
                Branch = "",
                WorkflowPath = "",
                Details = "No Managed Identity configuration found"
            });
        }
    }
}

