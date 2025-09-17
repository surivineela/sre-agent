// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using System.Text;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Graph.Crawler;
using Agent.Graph.Interfaces;
using Agent.Logging;
using Agent.Plugins;
using Agent.Plugins.Interface;
using Agent.Runtime.Communication;
using Agent.Runtime.Interfaces;
using Agent.Runtime.SubAgents.CVEAgent;
using Agent.Runtime.SubAgents.DailyReportSummary;
using Agent.Runtime.SubAgents.FeedbackRCAAgent;
using Agent.Runtime.SubAgents.LocalAuthAgent;
using Agent.Runtime.SubAgents.SourceCodeAgent;
using Agent.Runtime.SubAgents.TlsBestPracticesAgent;
using Agent.Runtime.SubAgents.WebAppDownAgent;
using Agent.Runtime.ThreadEvaluator;
using Agent.Runtime.TrajectoryEvaluator;

namespace Agent.Web.Services;


public class TimerService : IHostedService, IDisposable
{
    /// <summary>
    /// A class that stores information about a single scanner's timer
    /// </summary>
    private class ScannerTimerInformation
    {
        public string Name;
        public Timer? Timer { get; set; }
        public bool IsRunning { get; set; } = false;
        public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(1);
        public MethodInfo ScanMethod { get; }
        public object ScanTarget { get; set; } = null!;

        /// <summary>
        /// Create a new instance
        /// </summary>
        /// <param name="name">The name of this scanner</param>
        /// <param name="scanMethod">The method to initiate when timer is triggered</param>
        /// <param name="scanTarget">The instance on which to initiate it</param>
        public ScannerTimerInformation(string name, MethodInfo scanMethod, object scanTarget)
        {
            Name = name;
            ScanMethod = scanMethod;
            ScanTarget = scanTarget;
            IsRunning = false;
        }
    }

    private readonly ILogger<TimerService> _logger;
    private readonly ICrawlerService _crawlerService;
    private readonly IPostToTeamsPlugin _teamsPlugin;
    private readonly IGraphDBPlugin _graphPlugin;
    private readonly ChartPluginV2 _chartPlugin;
    private readonly IAgentInboundCommunicationService _agentInboundCommunicationService;
    private readonly IThreadRepository _repository;
    private readonly SinkService _sinkService;

    private CrawlerSettings _settings;
    private TimerSettings _timerSettings;
    private IncidentManagementSettings _incidentManagementSettings;
    private AgentMemorySettings _agentMemorySettings;
    private DashboardSettings _dashboardSettings;
    private TlsBestPracticesScanner _tlsBestPracticesScanner;
    private DailyReportScanner _dailyReportScanner;
    private SourceCodeScanner _sourceCodeScanner;
    private CVEScanner _cveScanner;
    private AppServiceScanner _appServiceScanner;
    private ScoreCardService _scoreCardService;
    private FeedbackRCAScanner _feedbackRCAScanner;
    private ThreadEvaluator _threadEvaluator;
    private TrajectoryEvaluator _trajectoryEvaluator;
    private CustomerLogger _customerLogger;
    private CustomerAuditLogger _customerAuditLogger;
    private LocalAuthScanner _localAuthScanner;
    private Agent.ScheduledTasks.Services.ScheduledTaskExecutionService _scheduledTaskExecutionService;

    private Timer? _crawlerTimer = null;
    private bool _crawlerTimerIsRunning = false;
    private bool _crawlerFinishedOnce = false;
    private volatile bool _dashboardImportedOnce = false;

    private Timer? _tlsTimer = null;
    private bool _tlsTimerIsRunning = false;
    private TimeSpan _tlsTimerInterval = TimeSpan.FromHours(1);

    private Timer? _dailyReportTimer = null;
    private bool _dailyReportTimerIsRunning = false;
    private TimeSpan _dailyReportTimerInterval = TimeSpan.FromHours(1); // daily report timer needs to attempt to run every hour so we can send it at 7am everyday
    private Timer? _sourceCodeCrawlerTimer = null;
    private bool _sourceCodeCrawlerTimerIsRunning = false;
    private TimeSpan _sourceCodeTimerInterval = TimeSpan.FromMinutes(1);

    private Timer? _cveCrawlerTimer = null;
    private bool _cveCrawlerTimerIsRunning = false;
    private TimeSpan _cveCrawlerTimerInterval = TimeSpan.FromMinutes(1);
    private bool _cveCrawlerFinishedOnce = false;

    private Timer? _appServiceCrawlerTimer = null;
    private bool _appServiceCrawlerTimerIsRunning = false;
    private TimeSpan _appServiceTimerInterval = TimeSpan.FromMinutes(10);

    private Timer? _scoreCardTimer = null;
    private bool _scoreCardTimerIsRunning = false;
    private TimeSpan _scoreCardTimerInterval = TimeSpan.FromMinutes(10);

    private Timer? _feedbackRCATimer = null;
    private bool _feedbackRCATimerIsRunning = false;
    private TimeSpan _feedbackRCATimerInterval = TimeSpan.FromMinutes(1);

    private Timer? _threadEvaluatorTimer = null;
    private bool _threadEvaluatorTimerIsRunning = false;
    private TimeSpan _threadEvaluatorTimerInterval = TimeSpan.FromMinutes(30);

    private Timer? _trajectoryEvaluatorTimer = null;
    private bool _trajectoryEvaluatorTimerIsRunning = false;
    private TimeSpan _trajectoryEvaluatorTimerInterval = TimeSpan.FromMinutes(30);

    private Timer? _githubAccessTokenTimer = null;
    private bool _githubAccessTokenTimerIsRunning = false;
    private TimeSpan _githubAccessTokenTimerInterval = TimeSpan.FromMinutes(1);

    private Timer? _pagerDutyWelcomeTimer;

    private Timer? _logFlushTimer = null;
    private bool _logFlushTimerIsRunning = false;
    private TimeSpan _logFlushTimerInterval = TimeSpan.FromSeconds(30);

    private Timer? _localAuthScannerTimer = null;
    private bool _localAuthScannerTimerIsRunning = false;
    private TimeSpan _localAuthScannerTimerInterval = TimeSpan.FromDays(1);

    private Timer? _scheduledTaskTimer = null;
    private bool _scheduledTaskTimerIsRunning = false;
    private TimeSpan _scheduledTaskTimerInterval = TimeSpan.FromMinutes(1);

    private List<ScannerTimerInformation> GenericSubAgentScannerTimers = new();

    private bool _pagerDutyWelcomeSent = false;

    private IIncidentScanner _incidentScanner;

    public TimerService(
        ICrawlerService crawlerService,
        CrawlerSettings settings,
        TimerSettings timerSettings,
        DashboardSettings dashboardSettings,
        IncidentManagementSettings incidentManagementSettings,
        AgentMemorySettings agentMemorySettings,
        IPostToTeamsPlugin teamsPlugin,
        TlsBestPracticesScanner tlsBestPracticesScanner,
        DailyReportScanner dailyReportScanner,
        SourceCodeScanner sourceCodeScanner,
        AppServiceScanner appServiceScanner,
        CVEScanner cveScanner,
        ILogger<TimerService> logger,
        IServiceProvider serviceProvider,
        IGraphDBPlugin graphPlugin,
        IAgentInboundCommunicationService agentInboundCommunicationService,
        IThreadRepository repository,
        ChartPluginV2 chartPlugin,
        ScoreCardService scoreCardService,
        SinkService sinkService,
        FeedbackRCAScanner feedbackRCAScanner,
        ThreadEvaluator threadEvaluator,
        TrajectoryEvaluator trajectoryEvaluator,
        CustomerLogger customerLogger,
        CustomerAuditLogger customerAuditLogger,
        IIncidentScanner incidentScanner,
        LocalAuthScanner localAuthScanner,
        Agent.ScheduledTasks.Services.ScheduledTaskExecutionService scheduledTaskExecutionService)
    {
        _logger = logger;
        _crawlerService = crawlerService;
        _graphPlugin = graphPlugin;
        _settings = settings;
        _repository = repository;
        _agentInboundCommunicationService = agentInboundCommunicationService;
        _chartPlugin = chartPlugin;
        _timerSettings = timerSettings;
        _incidentManagementSettings = incidentManagementSettings;
        _teamsPlugin = teamsPlugin;
        _tlsBestPracticesScanner = tlsBestPracticesScanner;
        _dailyReportScanner = dailyReportScanner;
        _sourceCodeScanner = sourceCodeScanner;
        _appServiceScanner = appServiceScanner;
        _cveScanner = cveScanner;
        _scoreCardService = scoreCardService;
        _sinkService = sinkService;
        _feedbackRCAScanner = feedbackRCAScanner;
        _dashboardSettings = dashboardSettings;
        _threadEvaluator = threadEvaluator;
        _trajectoryEvaluator = trajectoryEvaluator;
        _customerLogger = customerLogger;
        _customerAuditLogger = customerAuditLogger;
        _incidentScanner = incidentScanner;
        _localAuthScanner = localAuthScanner;
        _scheduledTaskExecutionService = scheduledTaskExecutionService;
        _agentMemorySettings = agentMemorySettings;

        _customerAuditLogger = customerAuditLogger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInternalInformation("Starting Log Flush timer...");
        StartLogFlushTimer(cancellationToken);

        if (_timerSettings.Disabled)
        {
            _logger.LogInternalWarning("Timer is disabled in appsettings. Skipping timer initialization.");
            return Task.CompletedTask;
        }
        _logger.LogInternalInformation($"Starting background services...");

        StartCrawlerTimer(cancellationToken);

        _logger.LogInternalInformation("Starting Send Welcome Message timer...");
        SendWelcomeMessageTimer(cancellationToken);

        _logger.LogInternalInformation($"Starting TLS timer...");
        StartTlsTimer(cancellationToken);

        _logger.LogInternalInformation("Starting Daily Report timer...");
        StartDailyReportTimer(cancellationToken);

        _logger.LogInternalInformation($"Starting Source Code timer...");
        //StartSourceCodeTimer(cancellationToken);

        _logger.LogInternalInformation($"Starting CVE timer...");
        StartCVETimer(cancellationToken);

        _logger.LogInternalInformation("Starting App Service timer...");
        //StartAppServiceTimer(cancellationToken);

        StartAllGenericSubAgentTimers(cancellationToken);

        _logger.LogInternalInformation($"Starting Score Card timer...");
        StartScoreCardTimer(cancellationToken);

        _logger.LogInternalInformation($"Finished starting background services");

        _logger.LogInternalInformation("Starting Feedback RCA timer...");
        StartFeedbackRCATimer(cancellationToken);

        _logger.LogInternalInformation("Starting Thread Evaluator timer...");
        StartThreadEvaluatorTimer(cancellationToken);

        if (_agentMemorySettings.Enabled)
        {
            _logger.LogInternalInformation("Starting Trajectory Evaluator timer...");
            StartTrajectoryEvaluatorTimer(cancellationToken);
        }

        _logger.LogInternalInformation("Starting GitHub access token timer...");
        //StartGitHubAccessTokenTimer(cancellationToken);

        _logger.LogInternalInformation("Starting Local Auth scanner timer...");
        StartLocalAuthScannerTimer(cancellationToken);

        _logger.LogInternalInformation("Starting Scheduled Task execution timer...");
        StartScheduledTaskTimer(cancellationToken);

        if (!string.IsNullOrEmpty(_incidentManagementSettings.Type?.ToString()))
        {
            _logger.LogInternalInformation($"Starting {_incidentManagementSettings.Type} Scanner timer ...");
            StartIncidentScannerTimer(cancellationToken);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInternalInformation("Stopping background services...");

        _crawlerTimer?.Change(Timeout.Infinite, 0); // Stop the timer
        _threadEvaluatorTimer?.Change(Timeout.Infinite, 0); // Stop the ThreadEvaluator timer
        _trajectoryEvaluatorTimer?.Change(Timeout.Infinite, 0); // Stop the TrajectoryEvaluator timer
        _localAuthScannerTimer?.Change(Timeout.Infinite, 0); // Stop the Local Auth scanner timer
        _scheduledTaskTimer?.Change(Timeout.Infinite, 0); // Stop the Scheduled Task timer

        // Stop all generic timers
        foreach (var scanner in GenericSubAgentScannerTimers)
        {
            scanner.Timer?.Change(Timeout.Infinite, 0);
        }

        return Task.CompletedTask;
    }

    public void StartIncidentScannerTimer(CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            await _incidentScanner.ScanAsync(cancellationToken);
        }, cancellationToken);
    }

    /// <summary>
    /// Kicks off the crawler every BackgroundCrawlIntervalInMinutes (60) minutes on a different thread
    /// </summary>
    public void StartCrawlerTimer(CancellationToken cancellationToken)
    {
        var random = new Random();
        // to avoid peak connections to graph on agent restarts
        var jitter = TimeSpan.FromMilliseconds(random.Next(1, 10000));
        _crawlerTimer = new Timer(async _ =>
        {
            if (_crawlerTimerIsRunning)
            {
                _logger.LogInternalInformation("Crawler is running. Skip this round");
                return; // Prevent overlapping executions
            }

            try
            {
                _crawlerTimerIsRunning = true;
                var roots = _settings.CrawlRoots.Split(",");
                await _crawlerService.CrawlAsync(roots, cancellationToken: cancellationToken);

                if (!_crawlerFinishedOnce)
                {
                    _crawlerFinishedOnce = true;
                    _crawlerService.StartActivityLogCrawler(roots, cancellationToken);
                    _logger.LogInternalInformation("Started activity log crawler");

                    await _crawlerService.StartKubernetesWatchCrawler(roots, cancellationToken);
                    _logger.LogInternalInformation("Started Kubernetes watch crawler");
                }

                await _crawlerService.DeleteStaleSoftDeletedNodes(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error executing crawler timer.");
            }
            finally
            {
                _crawlerTimerIsRunning = false; // Ensure flag resets even if CrawlAsync() fails
            }
        }, null, jitter, TimeSpan.FromMinutes(_timerSettings.BackgroundCrawlIntervalInMinutes));
    }

    public void StartTlsTimer(CancellationToken cancellationToken)
    {
        _tlsTimer = new Timer(async _ =>
        {
            if (_tlsTimerIsRunning)
            {
                _logger.LogInternalInformation("Tls best practice scanner is running. Skip this round");
                return; // Prevent overlapping executions
            }
            try
            {
                _tlsTimerIsRunning = true;
                await _tlsBestPracticesScanner.Scan(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error executing TLS timer.");
            }
            finally
            {
                _tlsTimerIsRunning = false;
            }
        }, null, TimeSpan.FromMinutes(5), _tlsTimerInterval);

    }

    public void StartSourceCodeTimer(CancellationToken cancellationToken)
    {
        _sourceCodeCrawlerTimer = new Timer(async _ =>
        {
            if (!_crawlerFinishedOnce)
            {
                _logger.LogInternalInformation("StartSourceCodeTimer: Resource crawler still in progress, wait for one round of scan to complete..");
                return; // Wait for the first crawl to finish
            }

            if (_sourceCodeCrawlerTimerIsRunning)
            {
                _logger.LogInternalInformation("Source code scanner is running. Skip this round");
                return; // Prevent overlapping executions
            }
            try
            {
                _sourceCodeCrawlerTimerIsRunning = true;
                await _sourceCodeScanner.Scan(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error executing source code timer.");
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
                _logger.LogInternalInformation("StartCVETimer: Resource crawler still in progress, wait for one round of scan to complete..");
                return; // Wait for the first crawl to finish
            }

            if (_cveCrawlerTimerIsRunning)
            {
                _logger.LogInternalInformation("CVE scanner is running. Skip this round");
                return; // Prevent overlapping executions
            }
            try
            {
                _cveCrawlerTimerIsRunning = true;
                await _cveScanner.Scan(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error executing CVE timer.");
            }
            finally
            {
                if (!_cveCrawlerFinishedOnce)
                {
                    _cveCrawlerFinishedOnce = true;
                }

                _cveCrawlerTimerIsRunning = false;
            }
        }, null, TimeSpan.FromMinutes(5), _cveCrawlerTimerInterval);

    }

    /// <summary>
    /// This starts all timers that derived from the shared base class.
    /// </summary>
    public void StartAllGenericSubAgentTimers(CancellationToken cancellationToken)
    {
        foreach (var scanner in GenericSubAgentScannerTimers)
        {
            _logger.LogInternalInformation($"Starting timer for {scanner.Name} scanner...");
            scanner.Timer = new Timer(async _ =>
            {
                if (scanner.IsRunning)
                {
                    _logger.LogInternalInformation($"Scanner '{scanner.Name}' is running. Skipping this round");
                    return; // Prevent overlapping executions
                }
                try
                {
                    _logger.LogInternalInformation($"Starting scanner '{scanner.Name}'");
                    scanner.IsRunning = true;
                    var result = scanner.ScanMethod.Invoke(scanner.ScanTarget, new object[] { cancellationToken });
                    if (result is Task task)
                    {
                        await task;
                    }
                    else
                    {
                        _logger.LogInternalWarning($"ScanMethod for '{scanner.Name}' did not return a Task.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, $"Error executing scanner '{scanner.Name}' timer.");
                }
                finally
                {
                    scanner.IsRunning = false;
                }
            }, null, TimeSpan.FromMinutes(1), scanner.Interval);
        }
    }

    public void StartDailyReportTimer(CancellationToken cancellationToken)
    {
        _dailyReportTimer = new Timer(async _ =>
        {
            if (!_dashboardImportedOnce)
            {
                _logger.LogInternalInformation("DailyReportTimer: Dashboard not imported yet, try to import it first.");
                var dashboardUrl = await _dailyReportScanner.TryToImportDashboards();
                _dashboardImportedOnce = true;
                _logger.LogInternalInformation("DailyReportTimer: Dashboard imported: {dashboardUrl}", dashboardUrl);
            }

            if (!_crawlerFinishedOnce || !_cveCrawlerFinishedOnce)
            {
                _logger.LogInternalInformation("DailyReportTimer: Resource crawler or CVE still in progress, wait for one round of scan to complete..");
                return; // Wait for the first crawl to finish
            }

            if (_dailyReportTimerIsRunning)
            {
                _logger.LogInternalInformation("Daily report scanner is already running. Skip this round.");
                return;
            }
            try
            {
                _dailyReportTimerIsRunning = true;
                var thread = await _dailyReportScanner.ScanAndGenerateReport(cancellationToken);
                if (thread == null)
                {
                    _logger.LogInternalInformation("No daily report generated.");
                    return;
                }
                //await _teamsPlugin.CreateTeamsThread(thread.Id.ToString(), thread.StartMessage.Text, thread.StartMessage.Id.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error executing daily report scanner.");
            }
            finally
            {
                _dailyReportTimerIsRunning = false;
            }
        }, null, TimeSpan.FromMinutes(5), _dailyReportTimerInterval);
    }

    public void StartScoreCardTimer(CancellationToken cancellationToken)
    {
        _scoreCardTimer = new Timer(async _ =>
        {
            if (!_crawlerFinishedOnce)
            {
                _logger.LogInternalInformation("StartScoreCardTimer: Resource crawler still in progress, wait for one round of scan to complete..");
                return;
            }

            if (_scoreCardTimerIsRunning)
            {
                _logger.LogInternalInformation("Score card update service is already running. Skip this round!");
                return;
            }
            try
            {
                _scoreCardTimerIsRunning = true;
                await _scoreCardService.UpdateAllScoreCardsAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error executing score card update service.");
            }
            finally
            {
                _scoreCardTimerIsRunning = false;
            }
        }, null, TimeSpan.Zero, _scoreCardTimerInterval);
    }

    public void StartAppServiceTimer(CancellationToken cancellationToken)
    {
        _appServiceCrawlerTimer = new Timer(async _ =>
        {
            if (_appServiceCrawlerTimerIsRunning)
            {
                _logger.LogInternalInformation("App service scanner is already running. Skip this round.");
                return;
            }
            try
            {
                _appServiceCrawlerTimerIsRunning = true;
                await _appServiceScanner.Scan(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error executing app service timer.");
            }
            finally
            {
                _appServiceCrawlerTimerIsRunning = false;
            }
        }, null, TimeSpan.Zero, _appServiceTimerInterval);
    }

    public void SendWelcomeMessageTimer(CancellationToken cancellationToken)
    {
        _pagerDutyWelcomeTimer = new Timer(async _ =>
        {
            if (_pagerDutyWelcomeSent)
            {
                _logger.LogInternalInformation("Welcome message already sent, skipping.");
                return;
            }

            try
            {
                var welcomeThreads = await _repository.GetThreadsBySourceAsync(null, ThreadSource.WelcomeMessage);
                if (welcomeThreads.Any())
                {
                    _logger.LogInternalInformation("Welcome message already sent, skipping.");
                    _pagerDutyWelcomeSent = true;
                    return;
                }

                var title = "Welcome to Azure SRE Agent";
                var messageBuilder = new StringBuilder();
                messageBuilder.AppendLine("Is there anything else I can help with today?");

                (var _, var agentContext) = await _agentInboundCommunicationService.CreateAgentThread(
                               title: title,
                               message: messageBuilder.ToString(),
                               agentTypeEnum: AgentTypeEnum.Meta,
                               source: ThreadSource.WelcomeMessage
                           );

                _logger.LogInternalInformation("PagerDuty welcome message sent successfully.");
                _pagerDutyWelcomeSent = true;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error sending PagerDuty welcome message.");
            }
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
    }

    public void StartFeedbackRCATimer(CancellationToken cancellationToken)
    {
        _feedbackRCATimer = new Timer(async _ =>
        {
            if (_feedbackRCATimerIsRunning)
            {
                _logger.LogInternalInformation("Feedback RCA scanner is already running. Skip this round.");
                return;
            }
            try
            {
                _feedbackRCATimerIsRunning = true;
                await _feedbackRCAScanner.Scan(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error executing feedback RCA scanner.");
            }
            finally
            {
                _feedbackRCATimerIsRunning = false;
            }
        }, null, TimeSpan.Zero, _feedbackRCATimerInterval);
    }

    public void StartThreadEvaluatorTimer(CancellationToken cancellationToken)
    {
        _threadEvaluatorTimer = new Timer(async _ =>
        {
            if (_threadEvaluatorTimerIsRunning)
            {
                _logger.LogInternalInformation("Thread evaluator is already running. Skip this round.");
                return;
            }
            try
            {
                _threadEvaluatorTimerIsRunning = true;
                await _threadEvaluator.Evaluate(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error executing thread evaluator.");
            }
            finally
            {
                _threadEvaluatorTimerIsRunning = false;
            }
        }, null, TimeSpan.Zero, _threadEvaluatorTimerInterval);
    }

    public void StartTrajectoryEvaluatorTimer(CancellationToken cancellationToken)
    {
        _trajectoryEvaluatorTimer = new Timer(async _ =>
        {
            if (_trajectoryEvaluatorTimerIsRunning)
            {
                _logger.LogInternalInformation("Trajectory evaluator is already running. Skip this round.");
                return;
            }
            try
            {
                _trajectoryEvaluatorTimerIsRunning = true;
                await _trajectoryEvaluator.Evaluate(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error executing trajectory evaluator.");
            }
            finally
            {
                _trajectoryEvaluatorTimerIsRunning = false;
            }
        }, null, TimeSpan.Zero, _trajectoryEvaluatorTimerInterval);
    }

    public void StartGitHubAccessTokenTimer(CancellationToken cancellationToken)
    {
        _githubAccessTokenTimer = new Timer(async _ =>
        {
            if (_githubAccessTokenTimerIsRunning)
            {
                _logger.LogInternalInformation("GitHub access token is already running. Skip this round.");
                return;
            }

            try
            {
                _githubAccessTokenTimerIsRunning = true;

                var gitHubAccessToken = await _repository.GetGitHubAccessTokenAsync();
                if (gitHubAccessToken != null
                    && gitHubAccessToken.ExpiresOn != null
                    && DateTime.UtcNow > gitHubAccessToken.ExpiresOn)
                {
                    _logger.LogInternalInformation("GitHub access token is expired. Skip this round.");
                    await _repository.DeleteGitHubAccessTokenAsync();
                }
                else
                {
                    _logger.LogInternalInformation("No github access token to delete");
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error executing github access token.");
            }
            finally
            {
                _githubAccessTokenTimerIsRunning = false;
            }
        }, null, TimeSpan.Zero, _githubAccessTokenTimerInterval);
    }

    public void StartLogFlushTimer(CancellationToken cancellationToken)
    {
        _logFlushTimer = new Timer(async _ =>
        {
            if (_logFlushTimerIsRunning)
            {
                Console.WriteLine("Log flusher is already running. Skip this round.");
                return;
            }

            _logFlushTimerIsRunning = true;

            Console.WriteLine("Flushing logs...");

            await Task.WhenAll(CustomerLoggerFlushAsync(cancellationToken),
                         CustomerAuditLoggerFlushAsync(cancellationToken));

            _logFlushTimerIsRunning = false;

        }, null, TimeSpan.Zero, _logFlushTimerInterval);
    }

    private async Task CustomerLoggerFlushAsync(CancellationToken cancellationToken)
    {
        if (_customerLogger == null)
        {
            Console.WriteLine("Application Insights logger is not initialized. Skip this round.");
            return;
        }

        try
        {
            await _customerLogger.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error executing application insightslog flusher.");
        }
    }

    private async Task CustomerAuditLoggerFlushAsync(CancellationToken cancellationToken)
    {
        if (_customerAuditLogger == null)
        {
            Console.WriteLine("Application Insights logger is not initialized. Skip this round.");
            return;
        }

        try
        {
            await _customerAuditLogger.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error executing application insightslog flusher.");
        }
    }

    public void StartLocalAuthScannerTimer(CancellationToken cancellationToken)
    {
        _localAuthScannerTimer = new Timer(async _ =>
        {
            if (!_crawlerFinishedOnce)
            {
                _logger.LogInternalInformation("StartLocalAuthScannerTimer: Resource crawler still in progress, wait for one round of scan to complete..");
                return; // Wait for the first crawl to finish
            }

            if (_localAuthScannerTimerIsRunning)
            {
                _logger.LogInternalInformation("Local Auth scanner is already running. Skip this round.");
                return;
            }
            try
            {
                _localAuthScannerTimerIsRunning = true;
                await _localAuthScanner.ScanAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error executing Local Auth scanner timer.");
            }
            finally
            {
                _localAuthScannerTimerIsRunning = false;
            }
        }, null, TimeSpan.FromMinutes(5), _localAuthScannerTimerInterval);
    }

    public void StartScheduledTaskTimer(CancellationToken cancellationToken)
    {
        _scheduledTaskTimer = new Timer(async _ =>
        {
            if (_scheduledTaskTimerIsRunning)
            {
                _logger.LogInternalInformation("Scheduled task executor is already running. Skip this round.");
                return;
            }
            try
            {
                _scheduledTaskTimerIsRunning = true;
                await _scheduledTaskExecutionService.ProcessDueTasksAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error executing scheduled task timer.");
            }
            finally
            {
                _scheduledTaskTimerIsRunning = false;
            }
        }, null, TimeSpan.FromMinutes(1), _scheduledTaskTimerInterval);
    }

    public void Dispose()
    {
        _logger.LogInternalInformation("Disposing Azure Resource Crawler Worker");

        _crawlerTimer?.Dispose();
        _threadEvaluatorTimer?.Dispose();
        _localAuthScannerTimer?.Dispose();
        _scheduledTaskTimer?.Dispose();

        // Dispose generic timers
        foreach (var scanner in GenericSubAgentScannerTimers)
        {
            scanner.Timer?.Dispose();
        }
    }
}
