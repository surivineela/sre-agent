// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Attributes;
using Agent.Plugins.Interface;

namespace Agent.Plugins
{
    public class AppIdentityUpdatePluginDefinition
    {
        private readonly IAppIdentityUpdatePlugin _appIdentityUpdatePlugin;

        public AppIdentityUpdatePluginDefinition(IAppIdentityUpdatePlugin appIdentityUpdatePlugin)
        {
            _appIdentityUpdatePlugin = appIdentityUpdatePlugin;
        }

        [Description(@"<category>Applicable if helping MI Migration or Identity SFI 1.6.2 or best practices</category>"
            + "Get's WebApp's Managed Identity if already enabled, otherwise enables managed identity and returns the same")]
        public async Task<string> GetAppManagedIdentityAsync(
            [Description("The resource ID of the App Service")]
            string resourceId)
        {
            return await _appIdentityUpdatePlugin.MigrateSqlToManagedIdentityAsync(resourceId);
        }

        [WriteAction]
        [RequiresApproval]
        [Description(@"<category>Applicable if helping MI Migration or Identity SFI 1.6.2 or best practices</category>"
            + "Migrates WebApp's SQL Connection String AppSetting to Managed Identity based Connection string\n\n"
            + "<important>Warning: This migration causes application downtime</important>\n"
            + "Prerequisites:\n"
            + "- Ensure managed identity is enabled on the webapp\n"
            + "- For next step, ie code migration to MI: Verify GitHub integration is configured, if not:\n"
            + "  - Request integration setup or direct code sharing\n"
            + "  - Confirm access status with user")]
        public async Task<string> MigrateWebAppConnStr2ManagedIdentityAsync(
            [Description("The resource ID of the App Service")]
            string resourceId,
            [Description("The SQL Server name")]
            string sqlServer,
            [Description("The database name")]
            string database)
        {
            return await _appIdentityUpdatePlugin.MigrateSqlToManagedIdentityAsync(resourceId, sqlServer, database);
        }

        [WriteAction]
        [RequiresApproval]
        [Description("<category>Applicable if helping MI Migration or Identity SFI 1.6.2 or best practices</category>"
            + "Enables Azure AD Entra Admin on the SQL Server if not already enabled. \n"
            + "This function gives the app admin access on the DB and hence Ensures the given identity has write permissions on the database \n"
            + "Should analyze user code for changes if CI/CD repo  after this step else suggest them changes. This Migration would cause a downtime")]
        public async Task<string> EnableSqlAdEntraAdminAsync(
            [Description("The SQL Server resource ID in a format /subscriptions/<subscription-id-of-app>/resourceGroups/<resourcegroupname>/providers/Microsoft.Sql/servers/<servername>, the value is provided by check_sql_resource_id")]
            string resourceId,
            [Description("The AAD application (service principal) object ID")]
            string servicePrincipalId)
        {
            return await _appIdentityUpdatePlugin.EnableSqlAdAuthAsync(resourceId, servicePrincipalId);
        }
    }
}
