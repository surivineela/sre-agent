using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

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
            _logger.LogInformation("Ensuring Teams message polling is started");
            _botPollingMessage.StartMessagePolling();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Teams message polling service stopping");
            _botPollingMessage.StopMessagePolling();  // This will properly cancel the polling
            return Task.CompletedTask;
        }
    }
}
