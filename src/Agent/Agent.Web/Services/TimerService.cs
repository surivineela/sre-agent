namespace Agent.Seb.Services;

using Agent.Core.Configuration;
using Agent.Graph.Crawler.ARM;
using Agent.Runtime.SubAgents;
using System.Text;

public class TimerService : IHostedService, IDisposable
{
    private readonly ILogger<TimerService> _logger;
    private readonly ResourceGraphCrawler _crawler;
    private CrawlerSettings _settings;
    private TimerSettings _timerSettings;
    private BestPracticeScannerAgent _bestPracticeScannerAgent;

    private Timer? _crawlerTimer = null;
    private bool _crawlerTimerIsRunning = false;
    private bool _crawlerFinishedOnce = false;

    private Timer? _bestPracticeTimer = null;
    private bool _bestPracticeTimerIsRunning = false;

    public TimerService(ResourceGraphCrawler crawler, CrawlerSettings settings, TimerSettings timerSettings, BestPracticeScannerAgent bestPracticeScannerAgent, ILogger<TimerService> logger)
    {
        _logger = logger;
        _crawler = crawler;
        _settings = settings;
        _timerSettings = timerSettings;
        _bestPracticeScannerAgent = bestPracticeScannerAgent;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Starting background services...");

        StartCrawlerTimer(cancellationToken);
        StartBestPracticeTimer(cancellationToken);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping background services...");

        _crawlerTimer?.Change(Timeout.Infinite, 0); // Stop the timer

        return Task.CompletedTask;
    }

    /// <summary>
    /// Kicks off the crawler every 30 minutes on a different thread
    /// </summary>
    public void StartCrawlerTimer(CancellationToken cancellationToken)
    {
        _crawlerTimer = new Timer(async _ =>
        {
            if (_crawlerTimerIsRunning) return; // Prevent overlapping executions

            try
            {
                _crawlerTimerIsRunning = true;
                await _crawler.Crawl(_settings.CrawlRoot, cancellationToken: cancellationToken);
                _crawlerFinishedOnce = true;
            }
            finally
            {
                _crawlerTimerIsRunning = false; // Ensure flag resets even if CrawlAsync() fails
            }
        }, null, TimeSpan.Zero, TimeSpan.FromMinutes(_timerSettings.BackgroundCrawlIntervalInMinutes));
    }

    /// <summary>
    /// Kicks off the crawler every 30 minutes on a different thread
    /// </summary>
    public void StartBestPracticeTimer(CancellationToken cancellationToken)
    {
        _bestPracticeTimer = new Timer(async _ =>
        {
            if (!_crawlerFinishedOnce) return; // Wait for the first crawl to finish

            if (_bestPracticeTimerIsRunning) return; // Prevent overlapping executions

            try
            {
                _bestPracticeTimerIsRunning = true;
                await _bestPracticeScannerAgent.Scan([], cancellationToken);
            }
            finally
            {
                _bestPracticeTimerIsRunning = false; // Ensure flag resets even if CrawlAsync() fails
            }
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(_timerSettings.BestPracticeScanIntervalInMinutes));
    }

    public void Dispose()
    {
        _logger.LogInformation("Disposing Azure Resource Crawler Worker");
        
        _crawlerTimer?.Dispose();
    }
}