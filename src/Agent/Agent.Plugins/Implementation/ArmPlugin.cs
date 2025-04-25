// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Core.Models;
using Agent.Plugins.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation
{
    public class ArmPlugin : IArmPlugin
    {
        private readonly ILogger<ArmPlugin> _logger;
        private readonly ArmHelper _armHelper;

        public ArmPlugin(ILogger<ArmPlugin> logger, ArmHelper armHelper)
        {
            _logger = logger;
            _armHelper = armHelper;
        }

        public async Task<string> SetMinimumTlsVersion(
            string appResourceId,
            string minimumTlsVersion)
        {
            var status = (await _armHelper.GetTlsSettings([appResourceId])).SingleOrDefault();
            bool success = false;
            string reason = string.Empty;

            if (status != null)
            {
                // Assume both versions are in the same format (e.g. "1.2" or "1.3").
                if (string.Equals(status.MinimumTlsVersion, minimumTlsVersion, StringComparison.InvariantCultureIgnoreCase))
                {
                    var msg = $"Resource {appResourceId} already has minimum TLS version set to {status.MinimumTlsVersion}. No action needed.";
                    _logger?.LogInformation(msg);
                    return msg;
                }

                var response = await _armHelper.UpdateMinimumTlsVersion(status, minimumTlsVersion);
                success = response.Item1;
                reason = response.Item2;
            }
            else
            {
                success = false;
                reason = $"Resource {appResourceId} not found.";
            }

            var message = success switch
            {
                true => $"Resource {appResourceId} updated with minimum TLS version set to {minimumTlsVersion} at {DateTime.UtcNow:o}",
                false => $"Failed to update resource {appResourceId} at {DateTime.UtcNow:o}. Reason: {reason}",
            };


            _logger?.LogInformation(message);
            return message;
        }
        public async Task<List<TlsStatus>> GetTlsSettings(List<string> resourceIds)
        {
            return await _armHelper.GetTlsSettings(resourceIds);
        }


        public async Task<bool> RestartWebApp(
            string appResourceId)
        {
            return await _armHelper.RestartWebAppAsync(appResourceId);
        }

        public async Task<bool> CheckIfResourceExists(string appResourceId)
        {
            return await _armHelper.CheckIfResourceExistsAsync(appResourceId);
        }

        public async Task<bool> RestartContainerApp(string appResourceId, string revisionName)
        {
            return await _armHelper.RestartContainerAppAsync(appResourceId, revisionName);
        }

        public async Task<string> GetArmResourceAsJson(string resourceId)
        {
            return await _armHelper.GetArmResourceAsJsonAsync(resourceId);
        }

        public async Task<RemediationResult> PowerOnVirtualMachine(string resourceId)
        {
            bool vmPowerOnResult = true;
            string message = "Virtual machine powered on successfully";
            try
            {
                vmPowerOnResult = await _armHelper.PowerOnVirtualMachineAsync(resourceId);
                if (!vmPowerOnResult)
                {
                    message = "Failed to power on the virtual machine";
                }
            }
            catch (Exception ex)
            {
                vmPowerOnResult = false;
                message = $"Error powering on the virtual machine: {ex.Message}";
        }

            return new RemediationResult(
                    Success: vmPowerOnResult,
                    Action: "Power On Azure Virtual Machine",
                    Details: message,
                    OperationId: null,
                    FinishedTime: DateTime.Now);
        }

        public async Task<IReadOnlyDictionary<string, string>> GetVirtualMachineBootDiagnostics(string resourceId)
        {
            var bootDiagnosticLogs = await _armHelper.GetVirtualMachineBootDiagnosticsAsync(resourceId);
            return bootDiagnosticLogs;
        }

        public async Task<string> CheckConnectivityToAzureWebJobsStorage(string resourceId)
        {
            return await _armHelper.CheckConnectivityToAzureWebJobsStorage(resourceId);
        }

        public async Task<string> CheckTcpConnectivity(string resourceId, string host, int port)
        {
            return await _armHelper.CheckTcpConnectivityAsync(resourceId, host, port);
        }

        public async Task<string> CheckDnsResolution(string resourceId, string desinationUrl)
        {
            return await _armHelper.CheckDnsResolution(resourceId, desinationUrl);
        }

        public async Task<IDictionary<string, string>> GetAppSetting(string resourceId, string appSettingKey)
        {
            return await _armHelper.GetAppSetting(resourceId, appSettingKey);
        }

        public async Task<IDictionary<string, string>> ListKeysForStorageAsync(string resourceId)
        {
            return await _armHelper.ListKeysForStorageAsync(resourceId);
        }

        public async Task<bool> UpdateAppSettingsAsync(string resourceId, IDictionary<string, string> appSettings)
        {
            return await _armHelper.UpdateAppSettingsAsync(resourceId, appSettings);
        }
    }
}

