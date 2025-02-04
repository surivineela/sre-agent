using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Azure.ResourceManager.Sql;
using Azure.ResourceManager.Sql.Models;
using Azure;

namespace Agents.Core.Plugins;

public class AppIdentityUpdatePlugin
{

    private const string AGENT_ID = "AppIdentityUpdatePlugin";
    private readonly ILogger<AppIdentityUpdatePlugin> _logger;

    public AppIdentityUpdatePlugin(ILogger<AppIdentityUpdatePlugin> logger)
    {
        _logger = logger;
    }

    [KernelFunction("migrate_sql_to_managed_identity")]
    [Description(@"<category>Identity SFI 1.6.2</category>
            Migrates SQL connection to Managed Identity and assigns DB Writer role.

            <important>Warning: This migration causes application downtime</important>

            Prerequisites:
            - Enables managed identity if not already enabled
            - For next step, ie code migration to MI: Verify GitHub integration is configured, if not:
              - Request integration setup or direct code sharing
              - Confirm access status with user")]
    public async Task<string> MigrateSqlToManagedIdentityAsync(
        [Description("The resource ID of the App Service")]
            string resourceId,
        [Description("The SQL Server name")]
            string sqlServer,
        [Description("The database name")]
            string database)
    {
        var notApprovedResult = RemediationPlugin.IsOperationNotApproved(operationName: "migrate_sql_to_managed_identity", resourceId: resourceId, out var approvalStatus);
        if (notApprovedResult is not null)
        {
            return notApprovedResult.Action + " " + notApprovedResult.Details;
        }

        var credential = new DefaultAzureCredential();
        var armClient = new ArmClient(credential);
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
            }

            // 2. Update connection string to use Managed Identity
            var appSettings = await webApp.GetApplicationSettingsAsync();
            var settings = appSettings.Value.Properties;

            var connectionString = $"Server={sqlServer};Database={database};" +
                "Authentication=Active Directory Default;TrustServerCertificate=True";

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
                   "Note: I need to next grant the app's managed identity 'db_datareader' and 'db_datawriter' " +
                   "roles in SQL Server using SQL commands.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error migrating to managed identity");
            throw;
        }
    }

    /*
    [KernelFunction("grant_sql_database_access")]
    [Description("<category>Only Applicable if user asks for Identity SFI 1.6.2</category>" +
        "Grants the app's managed identity db_datareader and db_datawriter roles on the SQL database. " +
         "<IMPORTANT>Should be called after migrate_sql_to_managed_identity.</IMPORTANT>" +
        "<important>IF ASKED TO ANALYZE REPO FIRST CHECK IF APP IS INTEGRATED WITH GITHUB</important>")]
    public async Task<string> GrantSqlDatabaseAccessAsync(
        [Description("The resource ID of the App Service")]
        string resourceId,
        [Description("The SQL Server name")]
        string sqlServer,
        [Description("The database name")]
        string database,
        [Description("Principal Id for the app")]
        string spId)
    {
        var notApprovedResult = RemediationPlugin.IsOperationNotApproved(operationName: "grant_sql_database_access", resourceId: resourceId, out var approvalStatus);
        if (notApprovedResult is not null)
        {
            return notApprovedResult.Action + " " + notApprovedResult.Details;
        }

        await Task.Yield();

        // First check and enable AD auth
        var sqlServerResourceId = $"/subscriptions/{resourceId.Split('/')[2]}/resourceGroups/" +
            $"{resourceId.Split('/')[4]}/providers/Microsoft.Sql/servers/{sqlServer}";

        var appResourceId = new ResourceIdentifier(resourceId);

        var sqlServerName = sqlServer.Split('.')[0];
        var sqlServerEndpoint = $"{sqlServerName}.database.windows.net";

        return $"Successfully granted db_datareader and db_datawriter roles to app's managed identity (Principal ID: {spId}) " +
                   $"on database {database}.";

        //var adAuthResult = await EnableSqlAdAuthAsync(sqlServerResourceId, spId);
        //if (!adAuthResult.Contains("enabled") && !adAuthResult.Contains("already"))
        //{
        //    return $"Failed to enable AD auth: {adAuthResult}";
        //}

        //var credential = new DefaultAzureCredential();
        //var armClient = new ArmClient(credential);
        //var armResourceId = new ResourceIdentifier(resourceId);

        //try
        //{
        //    // Get the app's managed identity
        //    var webAppResource = armClient.GetWebSiteResource(armResourceId);
        //    var webApp = await webAppResource.GetAsync();
        //    var appIdentity = webApp.Value.Data.Identity;

        //    if (appIdentity?.PrincipalId == null)
        //    {
        //        return "Error: App does not have a managed identity enabled. Please enable it first.";
        //    }

        //    // Connect to SQL using Azure AD auth
        //    var connectionString = $"Server={sqlServerEndpoint};Database={database};" +
        //        "Authentication=Active Directory Default;TrustServerCertificate=True";

        //    using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
        //    await connection.OpenAsync();

        //    // Grant permissions to the managed identity
        //    var principalId = appIdentity.PrincipalId.ToString();
        //    var commands = new[]
        //    {
        //        $"IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = '{appResourceId.Name}') " +
        //        $"CREATE USER [{appResourceId.Name}] FROM EXTERNAL PROVIDER;",

        //        $"ALTER ROLE db_datareader ADD MEMBER [{appResourceId.Name}];",

        //        $"ALTER ROLE db_datawriter ADD MEMBER [{appResourceId.Name}];"
        //    };

        //    foreach (var command in commands)
        //    {
        //        using var cmd = new Microsoft.Data.SqlClient.SqlCommand(command, connection);
        //        await cmd.ExecuteNonQueryAsync();
        //    }

        //    return $"Successfully granted db_datareader and db_datawriter roles to app's managed identity (Principal ID: {principalId}) " +
        //           $"on database {database}.";
        //}
        //catch (Exception ex)
        //{
        //    _logger.LogError(ex, "Error granting SQL database access");
        //    throw;
        //}
    }
    */

    [KernelFunction("enable_sql_ad_auth")]
    [Description("<category>Only Applicable if user asks for Identity SFI 1.6.2</category>" +
         "Enables Azure AD authentication on SQL Server if not already enabled. \n" +
         "Flow for complete Migration: Migrate App to SQL AD Auth, Enable AD Auth (and Entra Admin) on SQL Server, Change Connection String on App and Analyze code for changes\n" +
         "Ensures the given identity has write permissions on the database \n" +
         "should analyze user code for changes if CI/CD repo  after this step else suggest them changes. This Migration would cause a downtime")]
    public async Task<string> EnableSqlAdAuthAsync(
        [Description("The SQL Server resource ID in a format /subscriptions/<guid>/resourceGroups/<resourcegroupname>/providers/Microsoft.Sql/servers/<servername>")]
            string resourceId,
        [Description("The AAD application (service principal) object ID")]
            string servicePrincipalId)
    {
        var notApprovedResult = RemediationPlugin.IsOperationNotApproved(operationName: "enable_sql_ad_auth", resourceId: resourceId, out var approvalStatus);
        if (notApprovedResult is not null)
        {
            return notApprovedResult.Action + " " + notApprovedResult.Details;
        }

        // Stupid hack
        if (resourceId.EndsWith(".database.windows.net"))
        {
            resourceId = resourceId.Substring(0, resourceId.Length - (".database.windows.net").Length);
        }

        var credential = new DefaultAzureCredential();
        var armClient = new ArmClient(credential);
        var serverResourceId = new ResourceIdentifier(resourceId);

        try
        {
            var sqlServer = armClient.GetSqlServerResource(serverResourceId);

            // Get the collection of Azure AD administrators
            var adAdminCollection = sqlServer.GetSqlServerAzureADAdministrators();

            // Check if an Azure AD administrator already exists

            await foreach (var admin in adAdminCollection.GetAllAsync())
            {
                if (admin.Data.AdministratorType == SqlAdministratorType.ActiveDirectory &&
                    admin.Data.Login == servicePrincipalId)
                {
                    return "Azure AD authentication is already enabled on SQL Server.";
                }
            }

            // Retrieve tenant ID from an environment variable.
            // Alternatively, make this another method parameter if you wish.
            var tenantIdString = "72f988bf-86f1-41af-91ab-2d7cd011db47";
            if (string.IsNullOrEmpty(tenantIdString))
            {
                throw new InvalidOperationException(
                    "Cannot find AZURE_TENANT_ID in environment variables. " +
                    "Please set it or pass the tenant ID explicitly."
                );
            }

            // Construct AD admin parameters for the service principal
            var adminParams = new SqlServerAzureADAdministratorData
            {
                TenantId = Guid.Parse(tenantIdString),
                Login = servicePrincipalId,
                Sid = Guid.Parse(servicePrincipalId),
                AdministratorType = SqlAdministratorType.ActiveDirectory
            };

            // Set the Azure AD administrator for the SQL Server
            await adAdminCollection.CreateOrUpdateAsync(
                WaitUntil.Completed,
                SqlAdministratorName.ActiveDirectory,
                adminParams
            );

            return "Successfully enabled Azure AD auth on SQL Server and assigned the specified Service Principal.";
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Request failed while enabling SQL AD auth: {Message}", ex.Message);
            throw new Exception($"Request failed: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error enabling SQL AD auth: {Message}", ex.Message);
            throw new Exception($"Unexpected error: {ex.Message}", ex);
        }
    }
}

