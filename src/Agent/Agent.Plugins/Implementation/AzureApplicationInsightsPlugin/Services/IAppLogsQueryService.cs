// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Implementation.AzureApplicationInsightsPlugin.Services
{
    public interface IAppLogsQueryService
    {
        Task<IAppLogsQueryClient> CreateClientAsync();
    }
}
