// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Attributes;
using Agent.Core.Models;
using Agent.Framework;
using Agent.Plugins.Attributes;
using Agent.Plugins.Models;

namespace Agent.Plugins
{
    [AgentToolPlugin]
    public class ArmPluginDefinition
    {
        private readonly IArmPlugin _armPlugin;

        public ArmPluginDefinition(IArmPlugin armPlugin)
        {
            _armPlugin = armPlugin;
        }

        [Description("Gets the TLS settings for a list of resources.")]
        public async Task<List<TlsStatus>> GetTlsSettings(
            [Description("List of resource IDs to check the TLS minimum version for")]
            List<string> resourceIds)
        {
            return await _armPlugin.GetTlsSettings(resourceIds);
        }

        [Description("Checks if a resource exists in Azure.")]
        public async Task<bool> CheckIfResourceExists(
            [Description("The resource ID of the app.")]
            string appResourceId)
        {
            return await _armPlugin.CheckIfResourceExists(appResourceId);
        }

        [RequiresApproval]
        [Description("Sets the minimum TLS version on a site resource")]
        public async Task<string> SetMinimumTlsVersion(
            [Description("The resource ID of the app.")]
            string appResourceId,
            [Description("The minimum TLS version to set. Valid values: 1.2, 1.3")]
            string minimumTlsVersion)
        {
            return await _armPlugin.SetMinimumTlsVersion(appResourceId, minimumTlsVersion);
        }

        [RequiresApproval]
        [Description("Restart an AppService app")]
        public async Task<string> RestartWebApp(
            [Description("The resource ID of the AppService app.")]
            string appResourceId)
        {
            return await _armPlugin.RestartWebApp(appResourceId)
                ? "Restart succeeded"
                : "Restart failed";
        }

        [Description("Get ARM properties of a resource as JSON")]
        public async Task<string> GetArmResourceAsJson(
            [Description("Full resource id of an Azure resource")] string resourceId)
        {
            return await _armPlugin.GetArmResourceAsJson(resourceId);
        }

        [RequiresApproval]
        [Description("Power ON an Azure virtual machine")]
        public async Task<RemediationResult> PowerOnVirtualMachine(
            [Description("Full resource id of an Azure virtual machine resource")] string resourceId)
        {
            return await _armPlugin.PowerOnVirtualMachine(resourceId);
        }

        [Description("Get boot diagnostic logs and console screenshot for an Azure virtual machine")]
        public async Task<IReadOnlyDictionary<string, string>> GetVirtualMachineBootDiagnostics(
            [Description("Full resource id of an Azure virtual machine resource")] string resourceId)
        {
            return await _armPlugin.GetVirtualMachineBootDiagnostics(resourceId);
        }

        [Description("ONLY use when function app is using AzureWebJobsStorage connection string (NOT for managed identity). Tests connectivity from function app to its storage account.")]
        public async Task<string> CheckConnectivityToAzureWebJobsStorage(
            [Description("Full resource id of an Azure Function App")] string resourceId)
        {
            return await _armPlugin.CheckConnectivityToAzureWebJobsStorage(resourceId);
        }

        [Description("Check if a connection from the given resource to www.microsoft.com can be established.")]
        public async Task<string> CheckTcpConnectivity(
            [Description("Full resource id of an Azure resource")] string resourceId, 
            [Description("Host to test connectivity to")] string host, 
            [Description("Port to test connectivity to")] int port)
        {
            return await _armPlugin.CheckTcpConnectivity(resourceId, host, port);
        }

        [Description("Check if DNS resolution from the function app to the storage account's endpoint")]
        public async Task<string> CheckDnsResolution(
            [Description("Full resource id of an Azure resource")] string resourceId,
            [Description("The url of the target storage account's endpoint")] string destinationUrl)
        {
            return await _armPlugin.CheckDnsResolution(resourceId, destinationUrl);
        }

        [Description("Retrieves the key value pair for given App Setting key")]
        public async Task<IDictionary<string, string>> GetAppSetting(
            [Description("Full resource id of an Azure resource")] string resourceId,
            [Description("The App Setting key to look up")] string appSettingKey)
        {
            return await _armPlugin.GetAppSetting(resourceId, appSettingKey);
        }

        [RequiresApproval]
        [Description("Lists the keys for a given Azure Storage account and updates the specified App Setting in an App Service.")]
        public async Task<bool> ListKeysAndUpdateAppSettingsAsync(
            [Description("Full resource id of an Azure Storage account")] string storageResourceId,
            [Description("Full resource id of an Azure App Service")] string appServiceResourceId,
            [Description("The App Setting key to update with the storage account connection string")] string appSettingKey)
        {
            return await _armPlugin.ListKeysAndUpdateAppSettingsAsync(storageResourceId, appServiceResourceId, appSettingKey);
        }

        [RequiresApproval]
        [Description("Updates specific configuration values in the App Settings for a given Azure resource. Only use when explicitly asked to modify the Azure resource's configuration settings.")]
        public async Task<bool> UpdateAppSettingsAsync(
            [Description("Full resource id of an Azure resource")] string resourceId,
            [Description("Key-value pairs of App Settings to update (only include settings that need to be changed)")] IDictionary<string, string> appSettings)
        {
            return await _armPlugin.UpdateAppSettingsAsync(resourceId, appSettings);
        }

        [Description("""
Execute az commands for Azure resource read operations. Commands run IMMEDIATELY without approval.
USAGE: Provide complete az cli command string. ALWAYS specify --subscription parameter with valid subscriptionId/guid.
ALLOWED: Only 'list', 'show', 'get' commands.
EXAMPLES:
- List: 'az containerapp list -g MyRG --subscription <subId>'
- Show with query: 'az containerapp show -g MyRG -n MyApp --query properties.configuration.ingress --subscription <subId>'
BEST PRACTICES:
- Use only if no specific tool available
- Always include --subscription parameter
- Executes immediately - no approval needed
- Use to understand current state before changes
""")]
        public async Task<string> RunAzCliReadCommandsAsync(
    [Description("Complete az command string for read operations (list, show, get)")] string command)
        {
            return await _armPlugin.RunAzCliReadCommandsAsync(command);
        }

        [Description("""
Execute az commands for Azure resource write operations. Requires user approval before execution.
USAGE: Provide complete az cli command string. ALWAYS specify --subscription parameter with valid subscriptionId/guid.
ALLOWED: 'create', 'update', 'set', 'scale', 'start', 'stop', 'restart', 'add'
FORBIDDEN: 'delete', 'remove' commands NOT allowed for safety.
EXAMPLES:
- Create: 'az containerapp create -g MyRG -n MyApp --subscription <subId> --image myimage:latest'
- Update: 'az webapp update -g MyRG -n MyApp --set httpsOnly=true --subscription <subId>'
- Scale: 'az webapp scale -g MyRG -n MyApp --instance-count 3 --subscription <subId>'
BEST PRACTICES:
- Run read command first to understand current state
- Explain what will change
- Include rollback commands when possible
- Requires USER APPROVAL before execution
""")]
        public async Task<string> RunAzCliWriteCommandsAsync(
            [Description("Complete az command string for write operations (create, update, set, scale, start, stop, restart)")] string command)
        {
            return await _armPlugin.RunAzCliWriteCommandsAsync(command);
        }

        [Description("""
Get Azure CLI help information with optional text filtering. Used internally to validate and correct command syntax.
USAGE: Provide the Azure CLI command/topic to get help for, with optional search pattern to filter results.
PURPOSE: This tool helps the agent understand correct command syntax and parameters to fix invalid commands.
FILTERING: The optional pattern searches through the help text and returns only lines containing that text.
EXAMPLES:
- Get help for webapp: 'webapp'
- Get help for specific subcommand: 'webapp create'
- Filter help for location info: 'webapp create' with pattern 'location' (returns only help lines mentioning 'location')
- Filter for parameter info: 'containerapp' with pattern '--cpu' (returns only lines about CPU parameters)
NOTE: This is an internal tool for command validation, not for generating user documentation.
""")]
        public async Task<string> GetAzCliHelpAsync(
            [Description("The Azure CLI command/topic to get help for (e.g., 'webapp', 'containerapp create')")] string helpTopic,
            [Description("Optional search pattern to filter help output - returns only lines containing this text")] string grepPattern = null)
        {

            return await _armPlugin.GetAzCliHelpAsync(helpTopic, grepPattern);
        }
    }
}
