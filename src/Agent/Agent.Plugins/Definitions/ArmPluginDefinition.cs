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

        [Description("Check if all the associated resources can be reached from an Azure function app")]
        public async Task<string> CheckConnectivityViaConnectionString(
    [Description("Full resource id of an Azure Function App")] string resourceId)
        {
            return await _armPlugin.CheckConnectivityToAzureWebJobsStorage(resourceId);
        }

        [Description("Check if a connection from the given resource to www.microsoft.com can be established.")]
        public async Task<string> CheckTcpConnectivity(
            [Description("Full resource id of an Azure resource")] string resourceId, string host, int port)
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

        [Description("Lists the keys for a given Azure Storage account.")]
        public async Task<IDictionary<string, string>> ListKeysForStorageAsync(
            [Description("Full resource id of an Azure Storage account")] string resourceId)
        {
            return await _armPlugin.ListKeysForStorageAsync(resourceId);
        }

        [Description("Updates the App Settings for a given Azure resource.")]
        public async Task<bool> UpdateAppSettingsAsync(
            [Description("Full resource id of an Azure resource")] string resourceId,
            [Description("Key-value pairs of App Settings to update")] IDictionary<string, string> appSettings)
        {
            return await _armPlugin.UpdateAppSettingsAsync(resourceId, appSettings);
        }

        [Description("""
        Safely execute az commands to perform read operations on Azure resources.
        USAGE: Provide the complete az cli command as a string. Do not assume the default subscription the command will run against. ALWAYS specify the subscription id with --subscription parameter if needed. You should only provide one az command at a time.
        BASIC EXAMPLES:
        - List container apps: 'az containerapp list -g MyResourceGroup --subscription <subId>'
        ADVANCED EXAMPLES:
        - Get container app ingress property with query: 'az containerapp show -g MyResourceGroup -n MyContainerApp --query properties.configuration.ingress.external --subscription <subId>'
        BEST PRACTICES:
        - ONLY consider using this tool if you cannot find tools that more specific for your task.
        - ALWAYS specify the subscription with --subscription parameter if needed.
        - DO NOT use this tool for commands that modify the azure resource, e.g., commands contain 'create', 'update', 'delete', etc.
        """)]
        public async Task<string> RunAzCliReadCommandsAsync(
            [Description("Complete az command string")] string command)
        {
            return await _armPlugin.RunAzCliReadCommandsAsync(command);
        }
    }
}

