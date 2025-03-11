namespace Agent.Seb.Services;

using Agent.Core.Configuration;
using Agent.Graph.Crawler.ARM;
using Agent.Plugins.Definitions;
using Agent.Runtime.SubAgents;
using System.Text;

public class TimerService : IHostedService, IDisposable
{
    private readonly ILogger<TimerService> _logger;
    private readonly ResourceGraphCrawler _crawler;
    private readonly IPostToTeamsPlugin _teamsPlugin;
    private CrawlerSettings _settings;
    private TimerSettings _timerSettings;
    private BestPracticeScannerAgent _bestPracticeScannerAgent;

    private Timer? _crawlerTimer = null;
    private bool _crawlerTimerIsRunning = false;
    private bool _crawlerFinishedOnce = false;

    private Timer? _bestPracticeTimer = null;
    private bool _bestPracticeTimerIsRunning = false;

    private const int MaxRetries = 20;
    private const int RetryDelayMs = 10000;

    public TimerService(
        ResourceGraphCrawler crawler,
        CrawlerSettings settings,
        TimerSettings timerSettings,
        BestPracticeScannerAgent bestPracticeScannerAgent,
        IPostToTeamsPlugin teamsPlugin,
        ILogger<TimerService> logger)
    {
        _logger = logger;
        _crawler = crawler;
        _settings = settings;
        _timerSettings = timerSettings;
        _bestPracticeScannerAgent = bestPracticeScannerAgent;
        _teamsPlugin = teamsPlugin;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Starting background services...");

        StartCrawlerTimer(cancellationToken);

        _logger.LogInformation($"Starting best practice timer...");
        StartBestPracticeTimer(cancellationToken);

        _logger.LogInformation($"Finished starting background services");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping background services...");

        _crawlerTimer?.Change(Timeout.Infinite, 0); // Stop the timer
        _bestPracticeTimer?.Change(Timeout.Infinite, 0); // Stop the best practice timer

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
    /// Kicks off the best practice scanner and posts issues to Teams
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
                string? issues = await _bestPracticeScannerAgent.Scan([], cancellationToken);

                _logger.LogInformation("Best practice issues detected: {Issues}", issues);
                // If issues were found, post them to Teams
                if (!string.IsNullOrEmpty(issues))
                {
                    string messageToPost = $"⚠️ Best Practice Issues Detected ⚠️\n\n{issues}";
                    await PostToTeamsWithRetry(messageToPost);
                }
                else
                {
                    _logger.LogInformation("No best practice issues detected");
                    await PostToTeamsWithRetry("All best practices are met! 🎉");
                }

            }
            finally
            {
                _bestPracticeTimerIsRunning = false; // Ensure flag resets even if scan fails
            }
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(_timerSettings.BestPracticeScanIntervalInMinutes));
    }

    /// <summary>
    /// Posts a message to Teams with retry logic
    /// </summary>
    private async Task PostToTeamsWithRetry(string message)
    {
        int retryCount = 0;
        bool success = false;

        while (!success && retryCount < MaxRetries)
        {
            try
            {
                await _teamsPlugin.PostAsync(message);
                success = true;
                _logger.LogInformation("Successfully posted best practice issues to Teams");
            }
            catch (Exception ex)
            {
                retryCount++;
                if (retryCount >= MaxRetries)
                {
                    _logger.LogError(ex, "Failed to post best practice issues to Teams after {RetryCount} attempts", MaxRetries);
                    break;
                }

                _logger.LogWarning(ex, "Attempt {RetryCount} to post to Teams failed. Retrying in {DelayMs}ms",
                    retryCount, RetryDelayMs);

                await Task.Delay(RetryDelayMs);
            }
        }
    }

    public void Dispose()
    {
        _logger.LogInformation("Disposing Azure Resource Crawler Worker");

        _crawlerTimer?.Dispose();
        _bestPracticeTimer?.Dispose();
    }
}