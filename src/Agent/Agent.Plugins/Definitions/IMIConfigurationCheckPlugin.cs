// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;

namespace Agent.Plugins
{
    public interface IMIConfigurationCheckPlugin
    {
        Task<SqlConnectionDescriptor> CheckSqlConnectionTypeAsync(string resourceId);
        Task<string> CheckSqlResourceIdForAppAsync(string resourceId);
    }
}

