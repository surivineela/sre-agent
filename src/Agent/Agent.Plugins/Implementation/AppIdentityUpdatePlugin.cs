// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Logging;
using Agent.Plugins.Interface;
using Azure;
using Azure.Core;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Models;
using Azure.ResourceManager.Sql;
using Azure.ResourceManager.Sql.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation
{
    public class AppIdentityUpdatePlugin : IAppIdentityUpdatePlugin
    {
        private readonly ILogger<AppIdentityUpdatePlugin> _logger;
        private readonly IArmClientFactory _armClientFactory;

        public AppIdentityUpdatePlugin(ILogger<AppIdentityUpdatePlugin> logger, IArmClientFactory armClientFactory)
        {
            _armClientFactory = armClientFactory;
            _logger = logger;
        }

        public async Task<string> MigrateSqlToManagedIdentityAsync(string resourceId)
        {
            var armClient = await _armClientFactory.GetArmOperationClient();
            var armResourceId = new ResourceIdentifier(resourceId);

            try
            {
                // 1. Enable system-assigned managed identity if not enabled
                var webApp = armClient.GetWebSiteResource(armResourceId);
                var update = await webApp.GetAsync();

                if (update.Value.Data.Identity?.ManagedServiceIdentityType != ManagedServiceIdentityType.SystemAssigned)
                {
                    var patch = new Azure.ResourceManager.AppService.Models.SitePatchInfo()
                    {
                        Identity = new ManagedServiceIdentity(ManagedServiceIdentityType.SystemAssigned)
                    };
                    await webApp.UpdateAsync(patch);

                    update = await webApp.GetAsync();

                    return "Successfully enabled webapp to use Managed Identity. " +
                           $"Identity PrincipalId: {update.Value.Data.Identity.PrincipalId}.";
                }

                return "Webapp already had managed identity enabled" +
                       $"Identity PrincipalId: {update.Value.Data.Identity.PrincipalId}.";
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error migrating to managed identity");
                throw;
            }
        }

        public async Task<string> MigrateSqlToManagedIdentityAsync(string resourceId, string sqlServer, string database)
        {
            var armClient = await _armClientFactory.GetArmOperationClient();
            var armResourceId = new ResourceIdentifier(resourceId);

            try
            {
                var webApp = armClient.GetWebSiteResource(armResourceId);
                var update = await webApp.GetAsync();

                // Update connection string to use Managed Identity
                var appSettings = await webApp.GetApplicationSettingsAsync();
                var settings = appSettings.Value.Properties;

                var connectionString = GetConnectionString(sqlServer, database);

                // Find and update SQL connection strings
                bool updated = false;
                foreach (var key in settings.Keys.ToList())
                {
                    if (key.Contains("sql", StringComparison.OrdinalIgnoreCase) &&
                        settings[key].Contains("Password"))
                    {
                        settings[key] = connectionString;
                        updated = true;
                    }
                }

                if (!updated)
                {
                    settings["DefaultConnection"] = connectionString;
                }

                await webApp.UpdateApplicationSettingsAsync(appSettings.Value);

                return "Successfully migrated SQL connection to use Managed Identity. " +
                       $"Note: I need to to ensure I have SQL Admin set with Identity {update.Value.Data.Identity.PrincipalId}.";
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error migrating to managed identity");
                throw;
            }
        }

        public async Task<string> EnableSqlAdAuthAsync(string resourceId, string servicePrincipalId)
        {
            if (resourceId.EndsWith(".database.windows.net"))
            {
                resourceId = resourceId.Substring(0, resourceId.Length - (".database.windows.net").Length);
            }

            var armClient = await _armClientFactory.GetArmOperationClient();
            var serverResourceId = new ResourceIdentifier(resourceId);

            try
            {
                var sqlServer = armClient.GetSqlServerResource(serverResourceId);
                var adAdminCollection = sqlServer.GetSqlServerAzureADAdministrators();

                await foreach (var admin in adAdminCollection.GetAllAsync())
                {
                    if (admin.Data.AdministratorType == SqlAdministratorType.ActiveDirectory &&
                        admin.Data.Login == servicePrincipalId)
                    {
                        return "Azure AD authentication is already enabled on SQL Server.";
                    }
                }

                var tenantIdString = "72f988bf-86f1-41af-91ab-2d7cd011db47";
                if (string.IsNullOrEmpty(tenantIdString))
                {
                    throw new InvalidOperationException(
                        "Cannot find AZURE_TENANT_ID in environment variables. " +
                        "Please set it or pass the tenant ID explicitly."
                    );
                }

                var adminParams = new SqlServerAzureADAdministratorData
                {
                    TenantId = Guid.Parse(tenantIdString),
                    Login = servicePrincipalId,
                    Sid = Guid.Parse(servicePrincipalId),
                    AdministratorType = SqlAdministratorType.ActiveDirectory
                };

                await adAdminCollection.CreateOrUpdateAsync(
                    WaitUntil.Completed,
                    SqlAdministratorName.ActiveDirectory,
                    adminParams
                );

                return "Successfully enabled Azure AD auth on SQL Server and assigned the specified Service Principal.";
            }
            catch (RequestFailedException ex)
            {
                _logger.LogInternalError(ex, "Request failed while enabling SQL AD auth: {Message}", ex.Message);
                throw new Exception($"Request failed: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Unexpected error enabling SQL AD auth: {Message}", ex.Message);
                throw new Exception($"Unexpected error: {ex.Message}", ex);
            }
        }

        private string GetConnectionString(string server, string database)
        {
            string sqlServer = "";
            string dbName = database ?? "products";

            // Determine the correct server based on the name
            if (server.Contains("stage"))
            {
                sqlServer = "tcp:oa-demo-sql-stage.database.windows.net,1433";
            }
            else if (server.Contains("canary"))
            {
                sqlServer = "tcp:oa-demo-sql-canary.database.windows.net,1433";
            }
            else if (server.Contains("australiaeast"))
            {
                sqlServer = "tcp:oa-demo-sql-prod-australiaeast.database.windows.net,1433";
            }
            else if (server.Contains("swedencentral"))
            {
                sqlServer = "tcp:oa-demo-sql-prod-swedencentral.database.windows.net,1433";
            }
            else if (server.Contains("westus"))
            {
                sqlServer = "tcp:oa-demo-sql-prod-westus.database.windows.net,1433";
            }
            else if (server.Contains("sanchit"))
            {
                sqlServer = "tcp:oa-demo-sql-sanchit.database.windows.net,1433";
            }

            return $"Server={sqlServer};Initial Catalog={dbName};" +
                   "Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=\"Active Directory Default\"";
        }
    }
}
