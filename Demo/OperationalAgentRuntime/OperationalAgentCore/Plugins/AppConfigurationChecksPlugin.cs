using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace OperationalAgentCore
{
    public enum ConnectionType
    {
        None,
        Password,
        ManagedIdentity
    }

    public class AppConfigurationChecksPlugin
    {
        public sealed record SqlConnectionDescriptor(
            string sqlServerAddress,
            string databaseName,
            ConnectionType connectionType);

        [KernelFunction("check_sql_connection_type")]
        [Description("<category>Only Applicable if user asks for Identity SFI 1.6.2 or best practices</category>" +
            "Checks if an app service connects to a SQL server and whether it uses password or managed identity for authentication. Managed Identity Auth or Active Directory Default Authentication are same thing")]
        public async Task<SqlConnectionDescriptor> CheckSqlConnectionTypeAsync(
            [Description("The resource ID of the App Service.")]
            string resourceId)
        {
            Console.WriteLine($"[get_sql_connection_type] Invoked with resourceId: {resourceId}");

            var credential = new DefaultAzureCredential();
            var armClient = new ArmClient(credential);

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
                            var (serverName, dbName) = GetConnectionDetails(setting.Value);
                            return new SqlConnectionDescriptor(
                                sqlServerAddress: serverName,
                                databaseName: dbName,
                                connectionType: ConnectionType.Password);
                        }
                        else
                        {
                            return new SqlConnectionDescriptor(
                                sqlServerAddress: setting.Value,
                                databaseName: "Unknown", // For managed identity, we might need additional logic to determine DB name
                                connectionType: ConnectionType.ManagedIdentity);
                        }
                    }
                }

                return new SqlConnectionDescriptor(
                    sqlServerAddress: "None",
                    databaseName: "None",
                    connectionType: ConnectionType.None);
            }
            catch (RequestFailedException ex)
            {
                Console.Error.WriteLine($"Error in GetSqlConnectionTypeAsync: {ex.Message}");
                throw;
            }
        }

        private (string serverAddress, string databaseName) GetConnectionDetails(string connectionString)
        {
            string serverAddress = string.Empty;
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
                }
                else if (parameter.StartsWith("Initial Catalog=", StringComparison.OrdinalIgnoreCase) ||
                         parameter.StartsWith("Database=", StringComparison.OrdinalIgnoreCase))
                {
                    databaseName = parameter.Split('=')[1];
                }
            }

            return (serverAddress, databaseName);
        }
    }
}