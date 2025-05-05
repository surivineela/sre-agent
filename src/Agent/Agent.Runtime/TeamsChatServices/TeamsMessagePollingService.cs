// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Agent.Logging;

namespace Agent.Runtime.TeamsChatServices
{
    public class TeamsMessagePollingService : IHostedService
    {
        private readonly IBotPollingMessage _botPollingMessage;
        private readonly ILogger<TeamsMessagePollingService> _logger;

        public TeamsMessagePollingService(IBotPollingMessage botPollingMessage, ILogger<TeamsMessagePollingService> logger)
        {
            _botPollingMessage = botPollingMessage;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInternalInformation("Ensuring Teams message polling is started");
            _botPollingMessage.StartMessagePolling();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInternalInformation("Teams message polling service stopping");
            _botPollingMessage.StopMessagePolling();  // This will properly cancel the polling
            return Task.CompletedTask;
        }
    }
}

