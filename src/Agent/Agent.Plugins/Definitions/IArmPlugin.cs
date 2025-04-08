// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;

namespace Agent.Plugins
{
    public interface IArmPlugin
    {
        Task<string> SetMinimumTlsVersion(string appResourceId, string minimumTlsVersion);
        Task<List<TlsStatus>> GetTlsSettings(List<string> resourceIds);
        Task<bool> CheckIfResourceExists(string appResourceId);
        Task<bool> RestartWebApp(string appResourceId);
        Task<string> GetArmResourceAsJson(string resourceId);
        Task<string> PowerOnVirtualMachine(string resourceId);
        Task<IReadOnlyDictionary<string, string>> GetVirtualMachineBootDiagnostics(string resourceId);
    }
}

