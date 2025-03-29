using Agent.Core.Configuration;
using Agent.Graph.Crawler.ARM;
using Agent.Plugins.Definitions;
using Agent.Runtime.SubAgents;
using Agent.Runtime.SubAgents.DailyReportSummary;
using Agent.Runtime.SubAgents.CVEAgent;
using Agent.Runtime.SubAgents.SourceCodeAgent;
using Agent.Runtime.SubAgents.TlsBestPracticesAgent;
using ModelContextProtocol.Protocol.Types;

namespace Agent.Seb.Services;

public class TimerService : IHostedService, IDisposable
{
    private readonly ILogger<TimerService> _logger;
    private readonly ResourceGraphCrawler _crawler;
    private readonly IPostToTeamsPlugin _teamsPlugin;
    private CrawlerSettings _settings;
    private TimerSettings _timerSettings;
    private BestPracticeScannerAgent _bestPracticeScannerAgent;
    private TlsBestPracticesScanner _tlsBestPracticesScanner;
    private DailyReportScanner _dailyReportScanner;
    private SourceCodeScanner _sourceCodeScanner;
    private CVEScanner _cveScanner;

    private Timer? _crawlerTimer = null;
    private bool _crawlerTimerIsRunning = false;
    private bool _crawlerFinishedOnce = false;
    private int _crawlerTimerIntervalInSeconds = 30;

    private Timer? _bestPracticeTimer = null;
    private bool _bestPracticeTimerIsRunning = false;
    private int _bestPracticeTimerIntervalInMinutes = 24 * 60;

    private Timer? _tlsTimer = null;
    private bool _tlsTimerIsRunning = false;
    private TimeSpan _tlsTimerInterval = TimeSpan.FromMinutes(1);

    private Timer? _dailyReportTimer = null;
    private bool _dailyReportTimerIsRunning = false;
    private TimeSpan _dailyReportTimerInterval = TimeSpan.FromHours(24);
    private Timer? _sourceCodeCrawlerTimer = null;
    private bool _sourceCodeCrawlerTimerIsRunning = false;
    private TimeSpan _sourceCodeTimerInterval = TimeSpan.FromMinutes(1);

    private Timer? _cveCrawlerTimer = null;
    private bool _cveCrawlerTimerIsRunning = false;
    private TimeSpan _cveCrawlerTimerInterval = TimeSpan.FromMinutes(1);

    public TimerService(
        ResourceGraphCrawler crawler,
        CrawlerSettings settings,
        TimerSettings timerSettings,
        BestPracticeScannerAgent bestPracticeScannerAgent,
        IPostToTeamsPlugin teamsPlugin,
        TlsBestPracticesScanner tlsBestPracticesScanner,
        DailyReportScanner dailyReportScanner,
        ILogger<TimerService> logger,
        SourceCodeScanner sourceCodeScanner,
        CVEScanner cveScanner)
    {
        _logger = logger;
        _crawler = crawler;
        _settings = settings;
        _timerSettings = timerSettings;
        _bestPracticeScannerAgent = bestPracticeScannerAgent;
        _teamsPlugin = teamsPlugin;
        _tlsBestPracticesScanner = tlsBestPracticesScanner;
        _dailyReportScanner = dailyReportScanner;
        _sourceCodeScanner = sourceCodeScanner;
        _cveScanner = cveScanner;
        _bestPracticeTimerIntervalInMinutes = timerSettings.BestPracticeScanIntervalInMinutes;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Starting background services...");

        StartCrawlerTimer(cancellationToken);

        _logger.LogInformation($"Starting best practice timer...");
        //StartBestPracticeTimer(cancellationToken);

        //_logger.LogInformation($"Starting TLS timer...");
        StartTlsTimer(cancellationToken);

        _logger.LogInformation("Starting Daily Report timer...");
        StartDailyReportTimer(cancellationToken);

        _logger.LogInformation($"Starting Source Code timer...");
        StartSourceCodeTimer(cancellationToken);

        _logger.LogInformation($"Starting CVE timer...");
        StartCVETimer(cancellationToken);

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
            if (_crawlerTimerIsRunning)
            {
                _logger.LogInformation("Crawler is running. Skip this round");
                return; // Prevent overlapping executions
            }

            try
            {
                _crawlerTimerIsRunning = true;
                var roots = _settings.CrawlRoots.Split(",");
                int count = await _crawler.Crawl(roots, cancellationToken: cancellationToken);

                // Temp workaround for MVP demo
                // the UAMI might not have permission when the agent is created
                if (count > roots.Length)
                {
                    _crawlerFinishedOnce = true;
                    if (_crawlerTimerIntervalInSeconds != _timerSettings.BackgroundCrawlIntervalInMinutes * 60)
                    {
                        _logger.LogInformation("Crawled resources. Set timer to normal interval");
                        _crawlerTimerIntervalInSeconds = _timerSettings.BackgroundCrawlIntervalInMinutes * 60;
                        _crawlerTimer?.Change(
                            TimeSpan.FromSeconds(_crawlerTimerIntervalInSeconds),
                            TimeSpan.FromSeconds(_crawlerTimerIntervalInSeconds));
                    }
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error executing crawler timer.");
            }
            finally
            {
                _crawlerTimerIsRunning = false; // Ensure flag resets even if CrawlAsync() fails
            }
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(_crawlerTimerIntervalInSeconds));
    }

    public void StartTlsTimer(CancellationToken cancellationToken)
    {
        _tlsTimer = new Timer(async _ =>
        {
            if (_tlsTimerIsRunning)
            {
                _logger.LogInformation("Tls best practice scanner is running. Skip this round");
                return; // Prevent overlapping executions
            }
            try
            {
                _tlsTimerIsRunning = true;
                await _tlsBestPracticesScanner.Scan(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing TLS timer.");
            }
            finally
            {
                _tlsTimerIsRunning = false;
            }
        }, null, TimeSpan.Zero, _tlsTimerInterval);

    }

    public void StartSourceCodeTimer(CancellationToken cancellationToken)
    {
        _sourceCodeCrawlerTimer = new Timer(async _ =>
        {
            if (!_crawlerFinishedOnce)
            {
                _logger.LogInformation("StartSourceCodeTimer: Resource crawler still in progress, wait for one round of scan to complete..");
                return; // Wait for the first crawl to finish
            }

            if (_sourceCodeCrawlerTimerIsRunning)
            {
                _logger.LogInformation("Source code scanner is running. Skip this round");
                return; // Prevent overlapping executions
            }
            try
            {
                _sourceCodeCrawlerTimerIsRunning = true;
                await _sourceCodeScanner.Scan(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing source code timer.");
            }
            finally
            {
                _sourceCodeCrawlerTimerIsRunning = false;
            }
        }, null, TimeSpan.Zero, _sourceCodeTimerInterval);

    }

    public void StartCVETimer(CancellationToken cancellationToken)
    {
        _cveCrawlerTimer = new Timer(async _ =>
        {
            if (!_crawlerFinishedOnce)
            {
                _logger.LogInformation("StartCVETimer: Resource crawler still in progress, wait for one round of scan to complete..");
                return; // Wait for the first crawl to finish
            }

            if (_cveCrawlerTimerIsRunning)
            {
                _logger.LogInformation("CVE scanner is running. Skip this round");
                return; // Prevent overlapping executions
            }
            try
            {
                _cveCrawlerTimerIsRunning = true;
                await _cveScanner.Scan(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing CVE timer.");
            }
            finally
            {
                _cveCrawlerTimerIsRunning = false;
            }
        }, null, TimeSpan.Zero, _cveCrawlerTimerInterval);

    }

    /// <summary>
    /// Kicks off the best practice scanner and posts issues to Teams
    /// </summary>
    public void StartBestPracticeTimer(CancellationToken cancellationToken)
    {
        _bestPracticeTimer = new Timer(async _ =>
        {
            if (!_crawlerFinishedOnce)
            {
                _logger.LogInformation("StartBestPracticeTimer: Resource crawler still in progress, wait for one round of scan to complete..");
                return; // Wait for the first crawl to finish
            }

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
                    var succeed = await _teamsPlugin.CreateTeamsThread("", messageToPost);
                    if (succeed)
                    {
                        _bestPracticeTimerIntervalInMinutes = 60 * 24; // Set to 1 day
                        _bestPracticeTimer?.Change(
                            TimeSpan.FromMinutes(_bestPracticeTimerIntervalInMinutes),
                            TimeSpan.FromMinutes(_bestPracticeTimerIntervalInMinutes));
                        _logger.LogInformation("Best practice issues posted to Teams, will set the interval to 1 day");
                    }
                    else
                    {
                        _logger.LogError("Failed to post best practice issues to Teams");
                    }
                }
                else
                {
                    _logger.LogInformation("No best practice issues detected");
                    await _teamsPlugin.CreateTeamsThread("", "All best practices are met! 🎉");
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing best practice timer.");
            }
            finally
            {
                _bestPracticeTimerIsRunning = false; // Ensure flag resets even if scan fails
            }
        }, null,
        TimeSpan.FromMinutes(0), // Initial delay before first execution
        TimeSpan.FromMinutes(_bestPracticeTimerIntervalInMinutes)); // Interval between subsequent executions
    }

    public void StartDailyReportTimer(CancellationToken cancellationToken)
    {
        _dailyReportTimer = new Timer(async _ =>
        {
            if (_dailyReportTimerIsRunning)
            {
                _logger.LogInformation("Daily report scanner is already running. Skip this round.");
                return;
            }
            try
            {
                _dailyReportTimerIsRunning = true;
                var thread = await _dailyReportScanner.ScanAndGenerateReport(cancellationToken);
                if (thread == null)
                {
                    _logger.LogInformation("No daily report generated.");
                    return;
                }
                await _teamsPlugin.CreateTeamsThread(thread.Id.ToString(), thread.StartMessage.Text, thread.StartMessage.Id.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing daily report scanner.");
            }
            finally
            {
                _dailyReportTimerIsRunning = false;
            }
        }, null, TimeSpan.Zero, _dailyReportTimerInterval);
    }

    public void Dispose()
    {
        _logger.LogInformation("Disposing Azure Resource Crawler Worker");

        _crawlerTimer?.Dispose();
        _bestPracticeTimer?.Dispose();
    }
}
