// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Azure.Monitor.Query;

namespace Agent.Plugins.Implementation.AzureApplicationInsightsPlugin.Services
{
    public class AppLogsQueryService : IAppLogsQueryService
    {
        private readonly IAuthenticationService _authService;

        public AppLogsQueryService(IAuthenticationService authService)
        {
            _authService = authService;
        }

        public async Task<IAppLogsQueryClient> CreateClientAsync()
        {
            var credential = await _authService.GetArmOperationCredential();
            var options = new LogsQueryClientOptions();

            var client = new LogsQueryClient(credential, options);

            return new AppLogsQueryClient(client);
        }
    }
}
