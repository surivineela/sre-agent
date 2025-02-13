using Agent.Runtime;

namespace Agent.Web.Services
{
    public class SessionService : BackgroundService
    {
        private readonly Session _session;
        private readonly TimeSpan _period = TimeSpan.FromSeconds(5);

        public SessionService(Session conversation)
        {
            _session = conversation;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using PeriodicTimer timer = new(_period);
            while (
                !stoppingToken.IsCancellationRequested &&
                await timer.WaitForNextTickAsync(stoppingToken))
            {
                await DoWork(stoppingToken);
            }
        }

        private async Task DoWork(CancellationToken stoppingToken)
        {
            await _session.ProcessAsync(stoppingToken);
        }
    }
}
