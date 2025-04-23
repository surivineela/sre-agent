// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using System.Text;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Graph.Crawler;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Graph.Interfaces;
using Agent.Plugins.Definitions;
using Agent.Runtime.MetaAgent;
using Agent.Runtime.SubAgents;
using Agent.Runtime.SubAgents.CVEAgent;
using Agent.Runtime.SubAgents.DailyReportSummary;
using Agent.Runtime.SubAgents.SourceCodeAgent;
using Agent.Runtime.SubAgents.TlsBestPracticesAgent;
using Agent.Runtime.SubAgents.WebAppDownAgent;
using Gremlin.Net.Driver;
using Microsoft.Extensions.AI;
using Newtonsoft.Json;
using ArmConstants = Agent.Graph.Crawler.ARM.Constants;
using Agent.Plugins;
using Agent.Runtime.Communication;
using Agent.Logging;

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
    private readonly ChartPlugin _chartPlugin;
    private readonly IAgentInboundCommunicationService _agentInboundCommunicationService;
    private readonly IThreadRepository _repository;
    private readonly SinkService _sinkService;

    private CrawlerSettings _settings;
    private TimerSettings _timerSettings;
    private IncidentManagementSettings _incidentManagementSettings;
    private DashboardSettings _dashboardSettings;
    private BestPracticeScannerAgent _bestPracticeScannerAgent;
    private TlsBestPracticesScanner _tlsBestPracticesScanner;
    private DailyReportScanner _dailyReportScanner;
    private SourceCodeScanner _sourceCodeScanner;
    private CVEScanner _cveScanner;
    private AppServiceScanner _appServiceScanner;
    private ScoreCardService _scoreCardService;
    private FeedbackRCAScanner _feedbackRCAScanner;

    private Timer? _crawlerTimer = null;
    private bool _crawlerTimerIsRunning = false;
    private bool _crawlerFinishedOnce = false;

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

    private Timer? _appServiceCrawlerTimer = null;
    private bool _appServiceCrawlerTimerIsRunning = false;
    private TimeSpan _appServiceTimerInterval = TimeSpan.FromMinutes(10);

    private Timer? _scoreCardTimer = null;
    private bool _scoreCardTimerIsRunning = false;
    private TimeSpan _scoreCardTimerInterval = TimeSpan.FromMinutes(10);

    private Timer? _feedbackRCATimer = null;
    private bool _feedbackRCATimerIsRunning = false;
    private TimeSpan _feedbackRCATimerInterval = TimeSpan.FromMinutes(1);

    private List<ScannerTimerInformation> GenericSubAgentScannerTimers = new();

    private bool _pagerDutyWelcomeSent = false;
    private Timer? _pagerDutyWelcomeTimer = null;


    public TimerService(
        ICrawlerService crawlerService,
        CrawlerSettings settings,
        TimerSettings timerSettings,
        DashboardSettings dashboardSettings,
        IncidentManagementSettings incidentManagementSettings,
        BestPracticeScannerAgent bestPracticeScannerAgent,
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
        ChartPlugin chartPlugin,
        ScoreCardService scoreCardService,
        SinkService sinkService,
        FeedbackRCAScanner feedbackRCAScanner)
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
        _bestPracticeScannerAgent = bestPracticeScannerAgent;
        _teamsPlugin = teamsPlugin;
        _tlsBestPracticesScanner = tlsBestPracticesScanner;
        _dailyReportScanner = dailyReportScanner;
        _sourceCodeScanner = sourceCodeScanner;
        _appServiceScanner = appServiceScanner;
        _cveScanner = cveScanner;
        _scoreCardService = scoreCardService;
        _bestPracticeTimerIntervalInMinutes = timerSettings.BestPracticeScanIntervalInMinutes;
        _sinkService = sinkService;
        _feedbackRCAScanner = feedbackRCAScanner;
        _dashboardSettings = dashboardSettings;

        // Register all the scanners that implement this base type
        var scannerSubClasses = TypeReflectionHelpers.GetClassesDerivedFromGeneric(typeof(MetaAgent).Assembly, typeof(SimpleResourceSubAgentScannerBase<,,,>));
        foreach (var type in scannerSubClasses)
        {
            // Instantiate the type using DI
            var instance = serviceProvider.GetRequiredService(type);
            // Get a handle to the scan method
            var scanMethod = type.GetMethod("ScanAsync", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new Exception($"Could not find scanning method on type {type.Name}");
            // TODO: Would it be neater if instead of method+instance, we created an Action<Task> at this point?
            GenericSubAgentScannerTimers.Add(new ScannerTimerInformation(type.Name, scanMethod, instance));
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_timerSettings.Disabled)
        {
            _logger.LogWarning("Timer is disabled in appsettings. Skipping timer initialization.");
            return Task.CompletedTask;
        }
        _logger.LogInformation($"Starting background services...");

        StartCrawlerTimer(cancellationToken);

        _logger.LogInformation($"Starting best practice timer...");
        StartBestPracticeTimer(cancellationToken);

        _logger.LogInformation($"Starting TLS timer...");
        StartTlsTimer(cancellationToken);

        _logger.LogInformation("Starting Daily Report timer...");
        StartDailyReportTimer(cancellationToken);

        _logger.LogInformation($"Starting Source Code timer...");
        StartSourceCodeTimer(cancellationToken);

        _logger.LogInformation($"Starting CVE timer...");
        StartCVETimer(cancellationToken);

        _logger.LogInformation("Starting App Service timer...");
        //StartAppServiceTimer(cancellationToken);

        StartAllGenericSubAgentTimers(cancellationToken);

        _logger.LogInformation($"Starting Score Card timer...");
        StartScoreCardTimer(cancellationToken);

        _logger.LogInformation($"Finished starting background services");

        _logger.LogInformation("Starting Send Welcome Message timer...");
        SendWelcomeToPagerDutyMessageTimer(cancellationToken);

        _logger.LogInformation("Starting Feedback RCA timer...");
        StartFeedbackRCATimer(cancellationToken);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping background services...");

        _crawlerTimer?.Change(Timeout.Infinite, 0); // Stop the timer
        _bestPracticeTimer?.Change(Timeout.Infinite, 0); // Stop the best practice timer

        // Stop all generic timers
        foreach (var scanner in GenericSubAgentScannerTimers)
        {
            scanner.Timer?.Change(Timeout.Infinite, 0);
        }

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
                await _crawlerService.CrawlAsync(roots, cancellationToken: cancellationToken);

                if (!_crawlerFinishedOnce)
                {
                    _crawlerFinishedOnce = true;
                    _crawlerService.StartActivityLogCrawler(roots, cancellationToken);
                    _logger.LogInformation("Started activity log crawler");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing crawler timer.");
            }
            finally
            {
                _crawlerTimerIsRunning = false; // Ensure flag resets even if CrawlAsync() fails
            }
        }, null, TimeSpan.Zero, TimeSpan.FromMinutes(_timerSettings.BackgroundCrawlIntervalInMinutes));
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
            _logger.LogInternalInformation("Test internal log");
            _logger.LogExternalInformation("Test external log");

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
    /// This starts all timers that derived from the shared base class.
    /// </summary>
    public void StartAllGenericSubAgentTimers(CancellationToken cancellationToken)
    {
        foreach (var scanner in GenericSubAgentScannerTimers)
        {
            _logger.LogInformation($"Starting timer for {scanner.Name} scanner...");
            scanner.Timer = new Timer(async _ =>
            {
                if (scanner.IsRunning)
                {
                    _logger.LogInformation($"Scanner '{scanner.Name}' is running. Skipping this round");
                    return; // Prevent overlapping executions
                }
                try
                {
                    _logger.LogInformation($"Starting scanner '{scanner.Name}'");
                    scanner.IsRunning = true;
                    var task = (Task)scanner.ScanMethod.Invoke(scanner.ScanTarget, [cancellationToken]);
                    await task!;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error executing scanner '{scanner.Name}' timer.");
                }
                finally
                {
                    scanner.IsRunning = false;
                }
            }, null, TimeSpan.Zero, scanner.Interval);
        }
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

    public void StartScoreCardTimer(CancellationToken cancellationToken)
    {
        _scoreCardTimer = new Timer(async _ =>
        {
            if (!_crawlerFinishedOnce)
            {
                _logger.LogInformation("StartScoreCardTimer: Resource crawler still in progress, wait for one round of scan to complete..");
                return;
            }

            if (_scoreCardTimerIsRunning)
            {
                _logger.LogInformation("Score card update service is already running. Skip this round!");
                return;
            }
            try
            {
                _scoreCardTimerIsRunning = true;
                await _scoreCardService.UpdateAllScoreCardsAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing score card update service.");
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
                _logger.LogInformation("App service scanner is already running. Skip this round.");
                return;
            }
            try
            {
                _appServiceCrawlerTimerIsRunning = true;
                await _appServiceScanner.Scan(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing app service timer.");
            }
            finally
            {
                _appServiceCrawlerTimerIsRunning = false;
            }
        }, null, TimeSpan.Zero, _appServiceTimerInterval);
    }

    public void SendWelcomeToPagerDutyMessageTimer(CancellationToken cancellationToken)
    {
        _pagerDutyWelcomeTimer = new Timer(async _ =>
        {
            if (_pagerDutyWelcomeSent)
            {
                _logger.LogInformation("PagerDuty welcome message already sent, skipping.");
                return;
            }

            try
            {
                if (!_crawlerFinishedOnce)
                {
                    _logger.LogInformation("Waiting for first crawler run to complete before sending PagerDuty welcome message.");
                    return;
                }
                // STACY TO DO:
                // clean up code

                // STACY TO DO:
                // this is hard coded to check for pagerDutyLa logic app need to not hard code this
                // actually I need find another way to check the pagerDuty connection because I don't think logic apps are always discovered....
                // i think another way to do this is update logic app bicep to send a http call to trigger this timer idk

                //var pagerDutyLaQuery = "g.V().has('resourceName', 'pagerdutyla')";
                //var pagerDutyLaResult = await _graphPlugin.Query(pagerDutyLaQuery);

                //if (!pagerDutyLaResult.Any())
                //{
                //    _logger.LogInformation("PagerDuty Logic App resource not found in graph, skipping welcome message.");
                //    return;
                //}

                var welcomeThreads = await _repository.GetThreadsBySourceAsync(null, ThreadSource.WelcomeMessage);
                if (welcomeThreads.Any())
                {
                    _logger.LogInformation("Welcome message already sent, skipping.");
                    return;
                }

                var messageBuilder = new StringBuilder();
                messageBuilder.AppendLine("# 👋 Hi, I'm your new Azure SRE Partner!");
                messageBuilder.AppendLine();
                messageBuilder.AppendLine("I'm here to help monitor your applications and keep everything running smoothly.");

                if (_incidentManagementSettings != null && string.Equals(_incidentManagementSettings?.Type, "pagerduty", StringComparison.OrdinalIgnoreCase))
                {
                    messageBuilder.AppendLine("**I'm now connected to PagerDuty** and ready to process incidents for your environment.");
                }

                messageBuilder.AppendLine();
                messageBuilder.AppendLine("I've **already started scanning your applications** and will let you know shortly if I find anything that needs attention.");
                messageBuilder.AppendLine();
                messageBuilder.AppendLine("Think of me as your reliable sidekick for all things related to system reliability and operations. Whether you need help with security updates, monitoring metrics, or troubleshooting issues, I've got your back!");
                messageBuilder.AppendLine();
                messageBuilder.AppendLine("### ⚙️ **Autopilot Mode**:");
                messageBuilder.AppendLine();
                messageBuilder.AppendLine("I'm designed to work proactively on your behalf! From time to time, I'll notify you about important updates and ask for your approval before taking action. I'll continuously monitor your systems in the background, so you can focus on what matters most.");
                messageBuilder.AppendLine();

                if (_incidentManagementSettings != null && string.Equals(_incidentManagementSettings?.Type, "pagerduty", StringComparison.OrdinalIgnoreCase))
                {
                    messageBuilder.AppendLine("### 🚨 **PagerDuty Integration Active**:");
                    messageBuilder.AppendLine();
                    messageBuilder.AppendLine("With PagerDuty integration active, I can:");
                    messageBuilder.AppendLine("- Alert you about critical incidents in real-time");
                    messageBuilder.AppendLine("- Provide incident details and suggested resolutions");
                    messageBuilder.AppendLine("- Track incident status and resolution progress");
                    messageBuilder.AppendLine();
                }

                if (_dashboardSettings != null && !string.IsNullOrEmpty(_dashboardSettings.GrafanaUrl))
                {
                    messageBuilder.AppendLine("### 📊 **Azure Managed Grafana Integration Active**:");
                    messageBuilder.AppendLine();
                    messageBuilder.AppendLine("With Azure Managed Grafana integration, I can:");
                    messageBuilder.AppendLine("- Provide real-time visualization of your system metrics");
                    messageBuilder.AppendLine("- Help you track performance trends over time");
                    messageBuilder.AppendLine("- Create custom dashboards for your specific monitoring needs");
                    messageBuilder.AppendLine("- Send you links to relevant dashboards when troubleshooting issues");
                    messageBuilder.AppendLine();
                }

                messageBuilder.AppendLine("### **How to get started**:");
                messageBuilder.AppendLine();
                messageBuilder.AppendLine("If you have any specific questions or needs, simply mention what you'd like help with, and I'll jump right in. You can ask me to:");
                messageBuilder.AppendLine();
                messageBuilder.AppendLine("- \"Monitor my application performance\"");
                messageBuilder.AppendLine("- \"Check on my app's metrics\"");
                messageBuilder.AppendLine("- \"Create an app migration plan\"");
                messageBuilder.AppendLine("- \"Help diagnose why my service is slow\"");
                messageBuilder.AppendLine();
                messageBuilder.AppendLine("No fancy commands needed - just chat with me like you would with a colleague, and I'll help you tackle whatever challenges come your way.");
                messageBuilder.AppendLine();
                messageBuilder.AppendLine("Looking forward to working together and keeping your systems running at their best!");

                var title = "Azure SRE Partner Active";

                (var _, var agentContext) = await _agentInboundCommunicationService.CreateAgentThread(
                               title: title,
                               message: messageBuilder.ToString(),
                               agentTypeEnum: AgentTypeEnum.Meta,
                               source: ThreadSource.WelcomeMessage
                           );

                var chartPluginDefinition = new ChartPluginDefinition(_chartPlugin);
                _chartPlugin.ThreadId = agentContext.ThreadId;

                // count number of resources
                var totalResourceCountQuery = "g.V().dedup().by('id').count()";
                var totalResourceQueryResult = await _graphPlugin.Query(totalResourceCountQuery);
                var totalResourceCount = JsonConvert.SerializeObject(totalResourceQueryResult).Replace("[", "").Replace("]", "");

                // break down by type
                var resourceTypeCountQuery = "g.V().groupCount().by(label)";
                var resourceTypeCountQueryResult = await _graphPlugin.Query(resourceTypeCountQuery);
                var resourceTypeCountBreakDownQuery = $@"g.V().dedup().by('id').groupCount().by(
                                      coalesce(
                                        hasLabel('{ArmConstants.AppServiceType.ToLower()}').constant('App Service Web Apps'),
                                        hasLabel('{ArmConstants.ContainerAppType.ToLower()}').constant('Container Apps'),
                                        hasLabel('{ArmConstants.AzureSQLType.ToLower()}').constant('SQL'),
                                        hasLabel('{ArmConstants.CosmosDbType.ToLower()}').constant('Cosmos'),
                                        hasLabel('{ArmConstants.AzureRedisCacheType.ToLower()}').constant('Redis'),
                                        hasLabel('{ArmConstants.StorageType.ToLower()}').constant('Storage'),
                                        constant('Other')))";
                var resourceTypeCountBreakDownQueryResult = await _graphPlugin.Query(resourceTypeCountBreakDownQuery);
                var resourceTypeCountBreakDown = JsonConvert.SerializeObject(resourceTypeCountBreakDownQueryResult).Replace("[", "").Replace("]", "").Replace("{", "").Replace("}", "").Replace("\"", "");

                // Parse the breakdown and format it with each item on a new line
                var breakdownItems = resourceTypeCountBreakDown.Split(',')
                    .Select(item => item.Trim())
                    .Where(item => !string.IsNullOrEmpty(item))
                    .Select(item =>
                    {
                        var parts = item.Split(':');
                        return new { Type = parts[0].Trim(), Count = parts[1].Trim() };
                    })
                    .OrderBy(item => item.Type == "Other" ? 1 : 0) // Put "other" last
                    .ToList();

                // count number of all groups
                string appGroupTotalCountQuery = $@"g.V()
                        .out('{ArmConstants.Relationships.Contains}')
                        .out('{ArmConstants.Relationships.Contains}')
                        .hasLabel(within(
                            '{ArmConstants.ContainerAppType.ToLower()}',
                            '{ArmConstants.AppServiceType.ToLower()}',
                            '{ArmConstants.AzureKubernetesServiceType.ToLower()}',
                        ))
                        .dedup().by('id').count()";
                var appGroupTotalCountQueryResult = await _graphPlugin.Query(appGroupTotalCountQuery);
                var appGroupTotalCount = JsonConvert.SerializeObject(appGroupTotalCountQueryResult).Replace("[", "").Replace("]", "");

                // break down by type
                string appGroupTypeQuery = $@"g.V()
                        .out('{ArmConstants.Relationships.Contains}')
                        .out('{ArmConstants.Relationships.Contains}')
                        .hasLabel(within(
                            '{ArmConstants.ContainerAppType.ToLower()}',
                            '{ArmConstants.AppServiceType.ToLower()}',
                            '{ArmConstants.AzureKubernetesServiceType.ToLower()}',
                        ))
                        .groupCount().by(
                            coalesce(
                                hasLabel('{ArmConstants.ContainerAppType.ToLower()}').constant('Container Apps'),
                                hasLabel('{ArmConstants.AppServiceType.ToLower()}').constant('Web Apps'),
                                hasLabel('{ArmConstants.AzureKubernetesServiceType.ToLower()}').constant('Managed Clusters'),
                                label()
                            )
                        )";
                var appGroupsCountByTypeResult = await _graphPlugin.Query(appGroupTypeQuery);
                var appGroupsCountByType = JsonConvert.SerializeObject(appGroupsCountByTypeResult).Replace("[", "").Replace("]", "").Replace("{", "").Replace("}", "").Replace("\"", "");

                // Parse the breakdown and format it with each item on a new line
                var appGroupBreakdownItems = appGroupsCountByType.Split(',')
                    .Select(item => item.Trim())
                    .Where(item => !string.IsNullOrEmpty(item))
                    .Select(item =>
                    {
                        var parts = item.Split(':');
                        return new { Type = parts[0].Trim(), Count = parts[1].Trim() };
                    })
                    .ToList();

                var combinedDescription = new StringBuilder();
                combinedDescription.AppendLine("### **🔍 Here's what I found:**");
                combinedDescription.AppendLine();
                combinedDescription.AppendLine($"📊 Found **{totalResourceCount}** total resources. Here is a breakdown by type:");
                foreach (var item in breakdownItems)
                {
                    combinedDescription.AppendLine($"- **{item.Type}**: {item.Count}");
                }
                combinedDescription.AppendLine();
                combinedDescription.AppendLine($"📊 Found **{appGroupTotalCount}** app groups. Here is a breakdown by type:");
                foreach (var item in appGroupBreakdownItems)
                {
                    combinedDescription.AppendLine($"- **{item.Type}**: {item.Count}");
                }

                await _sinkService.SinkAgentMessageAsync(agentContext.ThreadId, combinedDescription.ToString());

                //// generate resource pie chart
                //var resourceTypeCountQueryResultDataPoint = JsonConvert.SerializeObject(resourceTypeCountBreakDownQueryResult).Replace("[", "").Replace("]", "").Replace("\\", "").Replace("/", "").Replace("{", "").Replace("}", "").Replace (":", "|").Replace(",",";");

                //var chartTitle = "Resource by Type";
                //var resourcePieChart = _chartPlugin.GetPieChartBase64Image(chartTitle, resourceTypeCountQueryResultDataPoint, "");
                //var resourcePieMessageString = $"![resource pie chart]({resourcePieChart})";
                //await _agentInboundCommunicationService.AppendAgentImageMessage(thread.Item2, resourcePieMessageString);

                //// generate app group pie chart
                //var appGroupsCountByTypeResultDataPoint = JsonConvert.SerializeObject(appGroupsCountByTypeResult).Replace("[", "").Replace("]", "").Replace("\\", "").Replace("/", "").Replace("{", "").Replace("}", "").Replace (":", "|").Replace(",",";");

                //// generate chart
                //var appGroupChartTitle = "App Groups by Type";

                //var appGroupResourcePieChart = _chartPlugin.GetPieChartBase64Image(appGroupChartTitle, appGroupsCountByTypeResultDataPoint, "");
                //var appGroupMessageString = $"![app group resource pie chart]({appGroupResourcePieChart})";

                //await _agentInboundCommunicationService.AppendAgentImageMessage(thread.Item2, appGroupMessageString);

                _logger.LogInformation("PagerDuty welcome message sent successfully.");
                _pagerDutyWelcomeSent = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending PagerDuty welcome message.");
            }
        }, null, TimeSpan.Zero, TimeSpan.FromMinutes(2));
    }

    public void StartFeedbackRCATimer(CancellationToken cancellationToken)
    {
        _feedbackRCATimer = new Timer(async _ =>
        {
            if (_feedbackRCATimerIsRunning)
            {
                _logger.LogInformation("Feedback RCA scanner is already running. Skip this round.");
                return;
            }
            try
            {
                _feedbackRCATimerIsRunning = true;
                await _feedbackRCAScanner.Scan(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing feedback RCA scanner.");
            }
            finally
            {
                _feedbackRCATimerIsRunning = false;
            }
        }, null, TimeSpan.Zero, _feedbackRCATimerInterval);
    }

    public void Dispose()
    {
        _logger.LogInformation("Disposing Azure Resource Crawler Worker");

        _crawlerTimer?.Dispose();
        _bestPracticeTimer?.Dispose();

        // Dispose generic timers
        foreach (var scanner in GenericSubAgentScannerTimers)
        {
            scanner.Timer?.Dispose();
        }
    }
}
