// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
using Agent.Plugins.Models;

namespace Agent.Plugins
{
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

        [Description("Sets the minimum TLS version on a site resource")]
        public async Task<string> SetMinimumTlsVersion(
            [Description("The resource ID of the app.")]
            string appResourceId,
            [Description("The minimum TLS version to set. Valid values: 1.2, 1.3")]
            string minimumTlsVersion)
        {
            return await _armPlugin.SetMinimumTlsVersion(appResourceId, minimumTlsVersion);
        }

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
        public async Task<string> CheckConnectivity(
    [Description("Full resource id of an Azure Function App")] string resourceId)
        {
            return await _armPlugin.CheckConnectivity(resourceId);
        }

        [Description("Check if a connection from the given resource to www.microsoft.com can be established.")]
        public async Task<string> CheckTcpConnectivity(
            [Description("Full resource id of an Azure resource")] string resourceId, string host, int port)
        {
            return await _armPlugin.CheckTcpConnectivity(resourceId, host, port);
        }

        [Description("Check if DNS resolution from the function app to the storage account's endpoint")]
        public async Task<string> CheckDnsResolution(
            [Description("Full resource id of an Azure resource and the url of the target storage account's endpoint")] string resourceId, string destinationUrl)
        {
            return await _armPlugin.CheckDnsResolution(resourceId, destinationUrl);
        }

        [Description("Retrieves the key value pair for given App Setting key")]
        public async Task<IDictionary<string, string>> FetchAppSetting(
            [Description("Full resource id of an Azure resource and the App Setting key to look up")] string resourceId, string appSettingKey)
        {
            return await _armPlugin.FetchAppSetting(resourceId, appSettingKey);
        }
    }
}

