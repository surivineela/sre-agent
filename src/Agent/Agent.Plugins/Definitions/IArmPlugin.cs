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
        Task<string> CheckConnectivity(string resourceId, string source, string destination, string destinationPort);
        Task<string> CheckTcpConnectivity(string resourceId, string host, int port);
    }
}

