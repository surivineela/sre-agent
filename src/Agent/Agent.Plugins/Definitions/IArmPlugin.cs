// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Plugins.Models;

namespace Agent.Plugins
{
    public interface IArmPlugin
    {
        Task<string> SetMinimumTlsVersion(string appResourceId, string minimumTlsVersion);
        Task<List<TlsStatus>> GetTlsSettings(List<string> resourceIds);
        Task<bool> CheckIfResourceExists(string appResourceId);
        Task<bool> RestartWebApp(string appResourceId);
        Task<string> GetArmResourceAsJson(string resourceId);
        Task<RemediationResult> PowerOnVirtualMachine(string resourceId);
        Task<IReadOnlyDictionary<string, string>> GetVirtualMachineBootDiagnostics(string resourceId);
        Task<string> CheckConnectivityToAzureWebJobsStorage(string resourceId);
        Task<string> CheckTcpConnectivity(string resourceId, string host, int port);
        Task<string> CheckDnsResolution(string resourceId, string destinationUrl);
        Task<IDictionary<string, string>> GetAppSetting(string resourceId, string appSettingKey);
        Task<IDictionary<string, string>> ListKeysForStorageAsync(string resourceId);
        Task<bool> UpdateAppSettingsAsync(string resourceId, IDictionary<string, string> appSettings);
        Task<string> RunAzCliReadCommandsAsync(string command);
    }
}

