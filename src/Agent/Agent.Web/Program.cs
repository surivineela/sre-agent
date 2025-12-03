// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using Agent.Core.Clients.Search;
using Agent.Core.Clients.Storage;
using Agent.Core.Configuration;
using Agent.Core.DataConnectors;
using Agent.Core.Extensions;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Core.Services.LinuxAppService.Validators;
using Agent.Data;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.Repositories;
using Agent.Framework;
using Agent.Framework.Skills;
using Agent.Graph.Crawler;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Crawler.Metrics;
using Agent.Graph.Interfaces;
using Agent.Graph.Services;
using Agent.Logging;
using Agent.Plugins;
using Agent.Plugins.Clients;
using Agent.Plugins.Connector;
using Agent.Plugins.Definitions;
using Agent.Plugins.Definitions.Connector;
using Agent.Plugins.IcmPlugin;
using Agent.Plugins.Implementation;
using Agent.Plugins.Implementation.AzureApplicationInsightsPlugin;
using Agent.Plugins.Implementation.AzureApplicationInsightsPlugin.Services;
using Agent.Plugins.Implementation.CdbSDKDiagnosePlugin;
using Agent.Plugins.Implementation.DiagnosticsPlugin;
using Agent.Plugins.Interface;
using Agent.Plugins.Kusto;
using Agent.Plugins.Python.Tools;
using Agent.Plugins.Services;
using Agent.Plugins.Services.Interfaces;
using Agent.Plugins.Tools;
using Agent.Prometheus.Services;
using Agent.Runtime;
using Agent.Runtime.AgentTasks;
using Agent.Runtime.Communication;
using Agent.Runtime.Extensions;
using Agent.Runtime.Heartbeat;
using Agent.Runtime.Helpers;
using Agent.Runtime.IncidentHandlerAgent;
using Agent.Runtime.Interfaces;
using Agent.Runtime.MetaAgent;
using Agent.Runtime.MetaAgent.Interfaces;
using Agent.Runtime.Reasoning;
using Agent.Runtime.Services;
using Agent.Runtime.Services.AzMonitorAlertInvestigation;
using Agent.Runtime.Services.AzMonitorAlertInvestigationService;
using Agent.Runtime.SubAgents;
using Agent.Runtime.SubAgents.CVEAgent;
using Agent.Runtime.SubAgents.DailySummaryAgent;
using Agent.Runtime.SubAgents.FeedbackRCAAgent;
using Agent.Runtime.SubAgents.LinuxAppServiceConfigAgent;
using Agent.Runtime.SubAgents.LocalAuthAgent;
using Agent.Runtime.SubAgents.SourceCodeAgent;
using Agent.Runtime.SubAgents.TlsBestPracticesAgent;
using Agent.Runtime.SubAgents.WebAppDownAgent;
using Agent.Runtime.TeamsChatServices;
using Agent.Runtime.ThreadEvaluator;
using Agent.Runtime.TrajectoryEvaluator;
using Agent.ScheduledTasks.Services;
using Agent.Web.Authorization;
using Agent.Web.Services;
using Agent.Web.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using Microsoft.SemanticKernel;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Agent.Web;

public class Program
{
    public static async Task Main(string[] args)
    {
        var app = CreateWebApplicationBuilder(new WebApplicationOptions { Args = args }).Build();

        var metricsService = app.Services.GetRequiredService<IGremlinMetricsService>();

        // Kick off metrics collection after the app has fully started
        app.Lifetime.ApplicationStarted.Register(() => metricsService.StartMetricsCollection());

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        // Add CORS support for Azure Portal domains
        app.UseCors(x => x.WithOrigins(GetAzurePortalDomains(app.Configuration))
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()
                  .SetIsOriginAllowedToAllowWildcardSubdomains());

        app.UseHttpsRedirection();
        // Serve static files from wwwroot
        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                var path = ctx.File.PhysicalPath ?? ctx.Context.Request.Path.Value ?? string.Empty;

                // Prevent index.html from being cached so users always get latest version
                if (path.EndsWith("index.html", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                }
            }
        });
        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapBlazorHub();

        // Map SignalR hub
        app.MapHub<Agent.Web.SignalR.AgentHub>("/agentHub");

        // Finally, map the fallback page
        app.MapFallbackToFile("/static/index.html");

        var azureSettings = app.Configuration.GetSection("AppSettings:Core:Azure").Get<AzureSettings>();
        var loggingSettings = app.Configuration.GetSection("Logging").Get<LoggingSettings>();

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            _ = Task.Run(async () =>
            {
                var logger = app.Services.GetRequiredService<ILogger<Program>>();
                logger.LogInternalInformation("Application started. Running startup tasks...");
                try
                {
                    logger.LogInternalInformation("Creating necessary cosmos containers...");
                    await app.Services.CreateCosmosContainerIfNotExists(app.Configuration);

                    logger.LogInternalInformation("Setting up Agent Memory...");
                    var agentMemorySettings = app.Configuration.GetSection("AppSettings:Core:AgentMemory").Get<AgentMemorySettings>();
                    if (agentMemorySettings is not null && agentMemorySettings.Enabled)
                    {
                        // Only attempt setup if all required settings are configured
                        if (IsAgentMemoryConfigurationValid(agentMemorySettings))
                        {
                            logger.LogInternalInformation("Agent Memory is enabled and configured. Setting up Agent Memory services...");
                            await app.Services.SetupAgentMemoryIndexAsync();
                            logger.LogInternalInformation("Agent Memory setup completed successfully.");
                        }
                        else
                        {
                            logger.LogInternalWarning("Agent Memory is enabled but required configuration settings are missing or invalid. Skipping Agent Memory setup.");
                        }
                    }
                    else
                    {
                        logger.LogInternalInformation("Agent Memory is not enabled. Skipping Agent Memory setup.");
                    }

                    logger.LogInternalInformation("Setting up Data Connector index...");
                    var dataConnectorIndex = app.Services.GetRequiredService<DataConnectorIndex>();
                    await dataConnectorIndex.CreateOrUpdateIndex();
                }
                catch (Exception ex)
                {
                    logger.LogInternalError(ex, "Error during application startup tasks");
                }
            });
        });

        await app.RunAsync();
    }

    public static WebApplicationBuilder CreateWebApplicationBuilder(WebApplicationOptions options)
    {
        return CreatePreliminaryWebApplicationBuilder(options);
    }

    private static WebApplicationBuilder CreatePreliminaryWebApplicationBuilder(WebApplicationOptions options)
    {
        var builder = WebApplication.CreateBuilder(options);

        var isAcaFirstPartyAgent = IsAcaFirstParty(options.Args ?? []);

        var agentType = Environment.GetEnvironmentVariable("AGENT_TYPE_NAME") ?? string.Empty;

        builder.LoadAppSettings(builder.Environment.IsDevelopment());
        builder.ValidateAndRegisterAppSettings<AppSettings>();

        // Configure Azure settings
        builder.Services.Configure<AzureSettings>(
            builder.Configuration.GetSection("Azure"));

        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

        // Add SignalR services
        builder.Services.AddSignalR(options =>
        {
            options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
            options.StreamBufferCapacity = 10;
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        });

        // Configure Azure App Insights settings
        builder.Services.Configure<AppInsightsSettings>(
            builder.Configuration.GetSection("AppInsightsSettings"));

        //Configure Azure Storage settings
        builder.Services.Configure<StorageSettings>(
            builder.Configuration.GetSection("AppSettings:Core:Azure:Storage"));

        // Configure Scheduled Task settings
        builder.Services.Configure<ScheduledTaskSettings>(
            builder.Configuration.GetSection("AppSettings:Core:Azure:ScheduledTasks"));

        // Configure Extended Agents Graph settings
        builder.Services.Configure<ExtendedAgentsGraphSettings>(
            builder.Configuration.GetSection("AppSettings:Core:Azure:ExtendedAgentsGraph"));

        // Configure Python Tool settings
        builder.Services.Configure<PythonToolSettings>(
            builder.Configuration.GetSection("AppSettings:Core:Azure:PythonTool"));

        // Configure MCP settings
        builder.Services.Configure<MCPSettings>(
            builder.Configuration.GetSection("AppSettings:Core:External:MCP"));

        // Configure AgentMemorySettings
        builder.Services.Configure<AgentMemorySettings>(
            builder.Configuration.GetSection("AppSettings:Core:AgentMemory"));

        // Add AgentMemorySettings singleton registration
        builder.Services.AddSingleton<AgentMemorySettings>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AgentMemorySettings>>();
            return options.Value;
        });

        // Configure ChatClientProvider settings
        builder.Services.Configure<ChatClientProviderSettings>(
            builder.Configuration.GetSection("AppSettings:Core:ChatClientProvider"));

        // Configure AgentModel settings
        builder.Services.Configure<AgentModelSettings>(
            builder.Configuration.GetSection("AppSettings:Core:AgentModel"));

        builder.Services.Configure<OpenAISettings>(
            builder.Configuration.GetSection("AppSettings:Core:Azure:OpenAI"));

        // Add AzureSearchSettings registration
        builder.RegisterAzureSearchSettings();

        // Add TsgCrawlerSettings registration right after the AzureSearchSettings registration
        builder.Services.AddSingleton<Agent.Core.Configuration.TsgCrawlerSettings>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            // Try to get from configuration first
            var settings = configuration.GetSection("AppSettings:Core:External:TSGCrawler").Get<Agent.Core.Configuration.TsgCrawlerSettings>();

            if (settings == null)
            {
                // Provide default settings to prevent null reference exceptions
                settings = new Agent.Core.Configuration.TsgCrawlerSettings
                {
                    Enabled = false,
                    AiSearchSettings = sp.GetRequiredService<Agent.Core.Configuration.AzureSearchSettings>()
                };
            }

            return settings;
        });

        // Register DiagnosticsHelper before ApplensService
        builder.Services.AddSingleton<Agent.Core.Helpers.DiagnosticsHelper>(sp =>
        {
            var authenticationService = sp.GetRequiredService<Agent.Core.Interfaces.IAuthenticationService>();
            var logger = sp.GetRequiredService<ILogger<Agent.Core.Helpers.DiagnosticsHelper>>();
            var hostEnvironment = sp.GetRequiredService<IHostEnvironment>();

            return new Agent.Core.Helpers.DiagnosticsHelper(logger, authenticationService, hostEnvironment);
        });

        // Register IApplensService with DiagnosticsHelper instead of HttpClient
        builder.Services.AddSingleton<Agent.Plugins.Services.Interfaces.IApplensService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Agent.Plugins.Services.ApplensService>>();
            var diagnosticsHelper = sp.GetRequiredService<Agent.Core.Helpers.DiagnosticsHelper>();

            return new Agent.Plugins.Services.ApplensService(logger, diagnosticsHelper);
        });

        // Register ApplensDetectorPlugin and its definition
        builder.Services.AddSingleton<Agent.Plugins.Interface.IApplensDetectorPlugin, Agent.Plugins.Implementation.ApplensDetectorPlugin>();
        builder.Services.AddTransient<Agent.Plugins.Definitions.ApplensDetectorPluginDefinition>();
        // Register DGrepPlugin client and definition
        builder.Services.AddSingleton<Agent.Plugins.Interface.IDGrepPluginClient, Agent.Plugins.Implementation.DGrepPluginClient>();
        builder.Services.AddTransient<Agent.Plugins.DGrepPluginDefinition>();
        // Add ExternalSettings registration after the other settings registrations
        builder.Services.AddSingleton<Agent.Core.Configuration.ExternalSettings>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var externalSettings = new Agent.Core.Configuration.ExternalSettings
            {
                // Use the already registered AzureSearchSettings and TsgCrawlerSettings
                AzureSearch = sp.GetRequiredService<Agent.Core.Configuration.AzureSearchSettings>(),
                TsgCrawler = sp.GetRequiredService<Agent.Core.Configuration.TsgCrawlerSettings>()
            };

            return externalSettings;
        });

        // Add JavaProfilerSettings registration
        builder.Services.AddSingleton<Agent.Core.Configuration.JavaProfilerSettings>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            // Try to get from configuration first
            var settings = configuration.GetSection("AppSettings:Core:Azure:JavaProfiler").Get<Agent.Core.Configuration.JavaProfilerSettings>();

            if (settings == null)
            {
                // Provide default settings to prevent null reference exceptions
                settings = new Agent.Core.Configuration.JavaProfilerSettings
                {
                    DebugProfileContainer = "",
                    ProfileTimeoutMinutes = 5
                };
            }

            return settings;
        });

        var azureSettings = builder.Configuration.GetSection("AppSettings:Core:Azure").Get<AzureSettings>();
        var agentModeString = azureSettings?.Action.Mode.ToString();

        // inject readonly configurator if readonly mode. For other modes pass the default (do nothing) for now
        if (string.Equals(agentModeString, "ReadOnly", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddSingleton<IAgentModeConfigurator<AgentContext>, ReadOnlyAgentModeConfigurator<AgentContext>>();
        }
        else if (string.Equals(agentModeString, "Autonomous", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddSingleton<IAgentModeConfigurator<AgentContext>, AutonomousAgentModeConfigurator<AgentContext>>();
        }
        else
        {
            builder.Services.AddSingleton<IAgentModeConfigurator<AgentContext>, DefaultAgentModeConfigurator<AgentContext>>();
        }
        // Register the AgentModeManager for runtime mode switching
        builder.Services.AddSingleton<IAgentModeManager<AgentContext>, AgentModeManager<AgentContext>>();
        builder.Services.AddSingleton<IAgentRuntimeModifier<AgentContext>, AgentRuntimeModifier>();

        // configure the allowed action header authentication as default scheme
        builder.Services.AddAuthentication(AllowedActionsAuthenticationConstants.SchemeName)
            .AddScheme<AllowedActionsAuthenticationSchemeOptions, AllowedActionsAuthenticationHandler>(
                AllowedActionsAuthenticationConstants.SchemeName,
                options =>
                {
                    options.ActionHeaderName = AllowedActionsAuthenticationConstants.ActionHeaderName;
                });
        builder.Services.AddScoped<IAuthorizationHandler, AuthorizeArmOperationHandler>();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, ArmOperationPolicyProvider>();

        builder.RegisterDataConnectors();

        // Register plugins and their dependencies
        builder.Services
            .AddSingleton<Agent.Runtime.MetaAgent.IAgent, MetaAgent>()
            .AddSingleton<IIncidentHandlerAgent, IncidentHandlerAgent>()
            .AddSingleton<IAppServicePlugin, AppServicePlugin>()
            .AddSingleton<AppServicePluginDefinition>()
            .AddSingleton<IFunctionAppsPlugin, FunctionAppsPlugin>()
            .AddSingleton<FunctionAppsPluginDefinition>()
            .AddSingleton<IGraphDatabaseClient, GremlinGraphDatabaseClient>()
            .AddSingleton<KustoPluginFactory>()
            .AddSingleton<ITimePlugin, TimePlugin>()
            .AddSingleton<IMetricsPlugin, MetricsPlugin>()
            .AddSingleton<IAppInsightsPlugin, AppInsightsPlugin>()
            .AddSingleton<AppInsightsPluginDefinition>()
            .AddSingleton<Agent.Plugins.Models.GitHubClient>()
            .AddSingleton<IAzureDevOpsWorkItemPlugin, AzureDevOpsWorkItemPlugin>()
            .AddSingleton<IAzureDevOpsSourceCodeSearch, AzureDevOpsSourceCodeSearch>()
            .AddSingleton<IOutlookConnectorPlugin, OutlookConnectorPlugin>()
            .AddTransient<IGithubIssuePlugin, GitHubIssuePlugin>()
            .AddTransient<ICustomerLogsPlugin, CustomerLogsPlugin>()
            .AddTransient<IWebAppRestartPlugin, WebAppRestartPlugin>()
            .AddTransient<WebAppRestartPluginDefinition>()
            .AddSingleton<IGithubFileService, GithubFileService>()
            .AddSingleton<ISourceCodeAnalysisPlugin, SourceCodeAnalysisPlugin>()
            .AddSingleton<IRemediationPlugin, RemediationPlugin>()
            .AddSingleton<AzureResourceGraphClient>()
            .AddSingleton<ArmHelper>()
            .AddSingleton<ILinuxAppServiceConfigValidator, LinuxFxVersionValidator>()
            .AddSingleton<AzureMonitorMetricsHelper>()
            .AddSingleton<PostgresSQLCommandHelper>()
            .AddSingleton<ArmResourceCrawlerFactory>()
            .AddSingleton<ICrawlerService, ResourceGraphCrawlerService>()
            .AddSingleton<ICrawlerTriggerService, CrawlerTriggerService>()
            .AddSingleton<IReliabilityPlugin, ReliabilityPlugin>()
            .AddSingleton<INSGRulePlugin, NSGRulePlugin>()
            .AddSingleton<IContainerAppPlugin, ContainerAppPlugin>()
            .AddSingleton<AzureSupportCenterHelper>()
            .AddSingleton<IAzureSupportCenterPlugin, AzureSupportCenterPlugin>()
            .AddSingleton<AppInsightsSettings>()
            .AddSingleton<IPrometheusQueryService, PrometheusQueryService>()
            .AddSingleton<IRoleAssignmentPlugin, RoleAssignmentPlugin>()
            .AddSingleton<IAppLogsQueryService, AppLogsQueryService>()
            .AddSingleton<ITeamsPlugin, TeamsPlugin>()
            .AddSingleton<TeamsPluginDefinition>()

            .AddTransient<IFunctionAppConfigurationChecksPlugin, FunctionAppConfigurationChecksPlugin>()

            .AddTransient<IFunctionAppDeploymentChecksPlugin, FunctionAppDeploymentChecksPlugin>()

            .AddTransient<IRunFromPackagePlugin, RunFromPackagePlugin>()
            .AddTransient<RunFromPackagePluginDefinition>()

            .AddTransient<IPostgreSQLPlugin, PostgreSQLPlugin>()
            .AddTransient<PostgreSQLPluginDefinition>()
            .AddTransient<IPostgreSQLAutomationPlugin, PostgreSQLAutomationPlugin>()
            .AddTransient<PostgreSQLAutomationPluginDefinition>()
            .AddSingleton<IPlaybookService, PlaybookService>()
            .AddTransient<IRedisPlugin, RedisPlugin>()
            .AddTransient<RedisPluginDefinition>()

            .AddSingleton<IMetricProviderHandler, GenevaMetricProviderHandler>()
            .AddSingleton<IMetricProviderHandler, AzureMonitorMetricProviderHandler>()
            .AddSingleton<MetricsAnalysisPluginDefinition>()
            .AddSingleton<IMetricsAnalysisPlugin, MetricsAnalysisPlugin>()
            .AddTransient<MetricsPluginDefinition>()
            .AddTransient<AzureMonitorMetricsPluginDefinition>()
            .AddTransient<ChartPluginDefinition>()
            .AddTransient<RecordActionsPluginDefinition>()
            .AddTransient<GrafanaPluginDefinition>()
            .AddTransient<GraphDBPluginDefinition>()
            .AddTransient<AzureActivityLogsPluginDefinition>()
            .AddTransient<AzureApplicationInsightsPluginDefinition>()
            .AddTransient<ArmPluginDefinition>()
            .AddTransient<CdbSDKDiagnosePluginDefinition>()
            .AddTransient<TimePluginDefinition>()
            .AddTransient<OutlookConnectorPluginDefinition>()
            .AddTransient<CustomerLogsPluginDefinition>()
            .AddTransient<MIConfigurationCheckPluginDefinition>()
            .AddTransient<GithubWorkflowTriggerPluginDefinition>()
            .AddTransient<RemediationPluginDefinition>()
            .AddTransient<AppIdentityUpdatePluginDefinition>()
            .AddTransient<ControlFlowPluginDefinition>()
            .AddTransient<NSGRulePluginDefinition>()
            .AddTransient<ContainerAppPluginDefinition>()
            .AddTransient<ReliabilityPluginDefinition>()
            .AddTransient<KubePluginDefinition>()
            .AddTransient<GitHubIssuePluginDefinition>()
            .AddTransient<AzureSupportCenterPluginDefinition>()
            .AddTransient<CpuAnalysisPluginDefinition>()
            .AddTransient<AppCodeAnalysisPluginDefinition>()
            .AddTransient<DiagnosticsPluginDefinition>()
            .AddTransient<RepositoryPluginDefintion>()
            .AddTransient<AzureDevOpsWorkItemPluginDefinition>()
            .AddTransient<SourceCodeAnalysisAgentPluginDefinition>()
            .AddTransient<RoleAssignmentPluginDefinition>()
            .AddTransient<CodeInterpreterPluginDefinition>()
            .AddTransient<PagerDutyIncidentPluginDefinition>()
            .AddTransient<FunctionAppExecutionFailuresPluginDefinition>()
            .AddTransient<FunctionAppConfigurationChecksPluginDefinition>()
            .AddTransient<FunctionAppDeploymentChecksPluginDefinition>()
            .AddTransient<LogicAppsPluginDefinition>()
            .AddTransient<FunctionsFlexConsumptionCRIPluginDefinition>()
            .AddTransient<AgentControlFlowPluginDefinition>()
            .AddTransient<AgentReasoningControlFlowPluginDefinition>()
            .AddTransient<UserInteractionPluginDefinition>()
            .AddTransient<APIManagementPluginDefinition>()
            .AddTransient<AgentMemoryPluginDefinition>()
            .AddTransient<GenevaActionsPluginDefinition>()
            .AddTransient<MdmMetricsPluginDefinition>()
            .AddTransient<ICMPluginDefinition>()
            .AddTransient<AzureAlertingPluginDefinition>()
            .AddTransient<WebAppPluginDefinition>()
            .AddTransient<IServiceNowPlugin, ServiceNowPlugin>()
            .AddTransient<ServiceNowPluginDefinition>()
            .AddTransient<ScheduledTaskPluginDefinition>()
            .AddTransient<KustoPlugin>()
            .AddSingleton<IKustoPlugin, KustoPlugin>()
            .AddSingleton<DynamicKqlToolsPlugin>()
            .AddTransient<KustoToolType>()
            .AddTransient<LinkToolType>()
            .AddTransient<PythonFunctionToolType>()
            .AddTransient<IAzureSearchClient, AzureSearchClient>()
            .AddTransient<AzureDocSearchPlugin>()
            .AddTransient<SearchPluginDefinition>()
            .AddTransient<ScaleControllerRCAPreflightPluginDefinition>()
            .AddTransient<BlobTriggerRCAPreflightPluginDefinition>()
            .AddTransient<ServiceBusTriggerRCAPreflightPluginDefinition>()
            .AddTransient<EventHubTriggerRCAPreflightPluginDefinition>()
            .AddTransient<RCAPreflightICMPluginDefinition>()
            .AddTransient<ColdStartPluginDefinition>()
            .AddTransient<LogsPluginDefinition>()
            .AddSingleton<AggregatedKustoToolsPluginDefinition>()
            .AddTransient<IKubeJavaPlugin, KubePluginJava>()
            .AddTransient<IKubePlugin, KubePlugin>()
            .AddTransient<IChartPlugin, ChartPluginV2>()
            .AddTransient<ChartPluginV2>()
            .AddTransient<IGraphDBPlugin, GraphDBPlugin>()
            .AddTransient<IAzureActivityLogsPlugin, AzureActivityLogsPlugin>()
            .AddTransient<IAzureApplicationInsightsPlugin, AzureApplicationInsightsPlugin>()
            .AddTransient<IPagerDutyIncidentPlugin, PagerDutyIncidentPlugin>()
            .AddTransient<IFunctionAppExecutionFailuresPlugin, FunctionAppExecutionFailuresPlugin>()
            .AddTransient<IAzureMonitorMetricsPlugin, AzureMonitorMetricsPlugin>()
            .AddTransient<IArmPlugin, ArmPlugin>()
            .AddTransient<ICodeInterpreterPlugin, CodeInterpreterPlugin>()
            .AddTransient<IAPIManagementPlugin, APIManagementPlugin>()
            .AddTransient<IGenevaActionsPlugin, GenevaActionsPlugin>()
            .AddTransient<IMdmMetricsPlugin, MdmMetricsPlugin>()
            .AddTransient<AgentHelperService>()
            .AddTransient<ILogicAppsPlugin, LogicAppsPlugin>()
            .AddTransient<ICdbSDKDiagnosePlugin, CdbSDKDiagnosePlugin>()

            .AddTransient<IICMPlugin, ICMPlugin>()
            .AddTransient<IAzureAlertingPlugin, AzureAlertingPlugin>()
            .AddTransient<IWebAppPlugin, WebAppPlugin>()
            .AddTransient<ICannotConnectToVmPlugin, CannotConnectToVmPlugin>()
            .AddTransient<CannotConnectToVmPluginDefinition>()

            .AddSingleton<ThreadEvaluator>();

        // Conditionally register InsightPostingService based on Session Insights enablement
        if (SessionInsightsEnabled(builder))
        {
            builder.Services.AddSingleton<InsightPostingService>();
        }

        builder.Services
            .AddSingleton<TlsBestPracticesScanner>()
            .AddTransient<LocalAuthScanner>()
            .AddSingleton<LinuxAppServiceConfigScanner>()
            .AddSingleton<SourceCodeScanner>()
            .AddSingleton<CVEScanner>()
            .AddSingleton<FeedbackRCAScanner>()
            .AddSingleton<ILogQueryService, LogQueryService>()
            .AddSingleton<IAzMonitorAlertInvestigationService, AzMonitorAlertInvestigationService>()
            .AddSingleton<IInvestigationOrchestrator, InvestigationOrchestrator>()
            .AddSingleton<IReflexionEvaluator, ReflexionEvaluator>()
            .AddSingleton<IReasoningStep, ApplicationHealthStep>()
            .AddSingleton<IReasoningStep, ActivityLogAnalysisStep>()
            .AddSingleton<IReasoningStep, ConnectedComponentsAnalysisStep>()
            .AddSingleton<IReasoningStep, LogQueryAnalysisStep>()
            .AddSingleton<IReasoningStep, MetricsAnalysisStep>()
            .AddSingleton<IReasoningStep, LogQueriesGenericAnalysisStep>()
            .AddSingleton<IHypothesisGenerator, HypothesisGenerator>()
            .AddSingleton<PostToTeamsPluginDefinition>()
            .AddSingleton<DailyReportScanner>()
            .AddSingleton<AppServiceScanner>()
            .AddSingleton<IPostToTeamsPlugin, PostToTeamsPlugin>()
            .AddSingleton<IConnectedIntegrationsPlugin, ConnectedIntegrationsPlugin>()
            .AddSingleton<IGrafanaPlugin, GrafanaPlugin>()
            .AddSingleton<IRecordActionsPlugin, RecordActionsPlugin>()
            .AddSingleton<IGithubWorkflowTriggerPlugin, GithubWorkflowTriggerPlugin>()
            .AddSingleton<IMIConfigurationCheckPlugin, MIConfigurationCheckPlugin>()
            .AddSingleton<IAppIdentityUpdatePlugin, AppIdentityUpdatePlugin>()
            .AddSingleton<ITimePlugin, TimePlugin>()
            .AddSingleton<IMcpConnectable, McpToolsRepository>()
            .AddSingleton<SinkService>()
            .AddSingleton<IStreamingMessageRepository, StreamingMessageRepository>()
            .AddSingleton<ThreadService>()
            .AddSingleton<ThreadManagementService>()
            .AddSingleton<IAgentInboundCommunicationService, InboundCommunicationService>()
            .AddSingleton<IStreamingService, SignalRStreamingService>()
            .AddSingleton<AgentTaskToolResultHelper>()
            .AddSingleton<IAgentOutboundCommunicationService, OutboundCommunicationService>()
            .AddSingleton<IApprovalService, ApprovalService>()
            .AddSingleton<IRemoteWriteService, RemoteWriteService>()
            .AddSingleton<IMetricsRegistry, MetricsRegistry>()
            .AddSingleton<IGremlinMetricsService, GremlinMetricsService>()
            .AddSingleton<DynamicIncidentManagementAgent>()
            .AddSingleton<AppInsightsPlugin>()
            .AddTransient<ICpuAnalysisPlugin, CpuAnalysisPlugin>()
            .AddTransient<IAppCodeAnalysisPlugin, AppCodeAnalysisPlugin>()
            .AddSingleton<IReasoningLoopFactory, ReasoningLoopFactory>()
            .AddSingleton<IReasoningLoopManager, ReasoningLoopManager>()
            .AddSingleton<ISearchPlugin, SearchPlugin>()
            // Code Optimizations plugin
            .AddTransient<ICodeOptimizationsPlugin, CodeOptimizationsPlugin>()
            .AddTransient<CodeOptimizationsPluginDefinition>()
            .AddSingleton<SearchHelper>()
            .AddSingleton<ISearchIndexingClient, SearchIndexingClient>()
            .AddSingleton<OneBranchApprovalService>()
            .AddSingleton<ICMWorkflowClient>()
            .AddSingleton<ISearchEndpointService, SearchEndpointService>()
            .AddSingleton<IKeyVaultService, KeyVaultService>()
            .AddSingleton<ObserverClientService>()
            .AddSingleton<IAzureBlobStorageClient, AzureBlobStorageClient>()
            .AddSingleton<IAzureDevOpsService, AzureDevOpsService>()
            .AddSingleton<IGitHubService, GitHubService>()
            .AddSingleton<ISessionPoolService, SessionPoolService>()
            .AddSingleton<IRagEvaluator, RagEvaluator>()
            .AddSingleton<IExtensibilityLoader, ExtensibilityLoader>()
            .AddSingleton(sp =>
            {
                return new ToolFactory<AgentContext>(
                    logger: sp.GetRequiredService<ILogger<ToolFactory<AgentContext>>>(),
                    serviceProvider: sp,
                    assembliesToScan: AppDomain.CurrentDomain.GetAssemblies()
                        .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                        .Where(assembly => assembly.GetName()?.Name?.StartsWith("Agent.") == true),
                    extensibilityLoader: sp.GetRequiredService<IExtensibilityLoader>(),
                    mcpToolsRepository: sp.GetRequiredService<IMcpConnectable>(),
                    skillRegistry: sp.GetRequiredService<ISkillRegistry>()
                    );
            })
            .AddSingleton<IToolFactory<AgentContext>, ToolFactory<AgentContext>>(sp =>
            {
                return sp.GetRequiredService<ToolFactory<AgentContext>>();
            })
            .AddSingleton<IAuthenticationService, AuthenticationService>()
            .AddSingleton<AgentToSkillService>()
            .AddCosmosClient();

        builder.Services.AddSingleton<ISkillRegistry>(sp =>
        {
            return new SkillRegistry(
                logger: sp.GetRequiredService<ILogger<SkillRegistry>>(),
                systemSkillsDirectory: Path.Combine(AppContext.BaseDirectory, "Skills"),
                extensibilityLoader: sp.GetRequiredService<IExtensibilityLoader>());
        });

        builder.Services.AddSingleton<IAgentFactory<AgentContext>>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<AgentFactory<AgentContext>>>();
            var toolFactory = sp.GetRequiredService<IToolFactory<AgentContext>>();
            var chatClientProvider = sp.GetRequiredService<IChatClientProvider>();
            var hostEnvironment = sp.GetRequiredService<IHostEnvironment>();
            var experimentalSettings = sp.GetRequiredService<ExperimentalSettings>();
            var modeConfigurator = sp.GetRequiredService<IAgentModeConfigurator<AgentContext>>();
            var extensibilityLoader = sp.GetRequiredService<IExtensibilityLoader>();
            var isGPT5Enabled = GPT5Enabled(builder);
            var agentMemoryRetrievalEnabled = AgentMemoryRetrievalEnabled(builder);
            // Get ScheduledTaskSettings to determine if scheduled tasks should be enabled
            var scheduledTaskSettings = sp.GetRequiredService<IConfiguration>()
                .GetSection("AppSettings:Core:Azure:ScheduledTasks")
                .Get<ScheduledTaskSettings>();
            var isScheduledTaskEnabled = scheduledTaskSettings?.Enabled ?? false;

            // Build promptStarters array conditionally based on scheduled task enablement
            var promptStarters = new List<string> { Core.Constants.SREAgentPromptStarter };
            if (isScheduledTaskEnabled)
            {
                promptStarters.Add(Core.Constants.ScheduledTaskInstructions);
            }

            return new AgentFactory<AgentContext>(
                logger: logger,
                toolFactory: toolFactory,
                chatClientProvider: chatClientProvider,
                assembliesToScan: AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                    .Where(assembly => assembly.GetName()?.Name?.StartsWith("Agent.Runtime") == true),
                modeConfigurator: modeConfigurator,
                agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "AgentsV2"),
                commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "CommonPrompts"),
                commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "CommonTools"),
                experimentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Experiments"),
                promptStarters: promptStarters.ToArray(),
                promptEnders: [Core.Constants.SREAgentFinalInstructions],
                defaultOutputType: typeof(DefaultAgentOutput),
                enableHandoffReasoning: experimentalSettings?.EnableHandoffReasoning ?? hostEnvironment.IsDevelopment(),
                extensibiltyLoader: extensibilityLoader,
                gpt5Enabled: isGPT5Enabled,
                agentMemoryRetrievalEnabled: agentMemoryRetrievalEnabled,
                scheduledTasksEnabled: isScheduledTaskEnabled,
                dynamicAgentDescriptors: [
                    () =>
                    {
                        var dynamicIncidentManagementAgent = sp.GetRequiredService<DynamicIncidentManagementAgent>();
                        return dynamicIncidentManagementAgent.GetIncidentManagementAgentDescriptor();
                    }
                ]);
        });

        // Register AgentProvider as singleton for per-thread experiment variant support
        builder.Services.AddSingleton<IAgentProvider<AgentContext>>(sp =>
        {
            var agentFactory = sp.GetRequiredService<IAgentFactory<AgentContext>>();
            var variantAssigner = sp.GetRequiredService<IVariantAssigner>();
            var logger = sp.GetRequiredService<ILogger<AgentProvider<AgentContext>>>();
            var instanceId = Environment.GetEnvironmentVariable("AGENT_NAME") ?? Core.Constants.DefaultAgentName;

            string? forceEnabledExperiments = null;

            if (ShouldForceEnableSkills(builder))
            {
                logger.LogInternalInformation("Forcing skill enablement.");
                forceEnabledExperiments = "agent_skills=single_agent_with_skills";
            }

            return new AgentProvider<AgentContext>(
                factory: agentFactory,
                variantAssigner: variantAssigner,
                logger: logger,
                instanceId: instanceId,
                runtimeForcedVariants: forceEnabledExperiments);
        });

        // Register IVariantAssigner
        builder.Services.AddSingleton<IVariantAssigner, HashVariantAssigner>();

        builder.Services.ConfigureFrameworkAsyncInitializers<AgentContext>();

        builder.Services
            .AddSingleton<IDiagnosticsPlugin, DiagnosticsPlugin>()
            .AddSingleton<ISearchPlugin, SearchPlugin>()

            .AddSingleton<Octokit.IGitHubClient>(provider =>
            {
                var client = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("AzureSreAgent"));
                return client;
            })
            .AddTransient<Kernel>(sp => new Kernel(sp))
            // Register all SubAgent types as singletons
            .AddSingleton<GraphDBQueryAgent>()
            .AddSingleton<ArchitectureAgent>()
            .AddSingleton<LogsAndMetricsAgent>()

            // Register Metrics collectors
            .AddSingleton<ScoreCardService>()
            .AddSingleton<IAzureMetricsClient, AzureMetricsClient>()
            .AddSingleton<IPrometheusEndpointService, PrometheusEndpointService>()
            .AddSingleton<IResourceMetricsCollector, ContainerAppMetricsCollector>()
            .AddSingleton<IResourceMetricsCollector, FunctionAppMetricsCollector>()
            .AddSingleton<IResourceMetricsCollector, AppServiceMetricsCollector>()
            .AddSingleton<IResourceMetricsCollector, RedisMetricsCollector>()
            .AddSingleton<IResourceMetricsCollector, AKSMetricsCollector>()
            .AddSingleton<IResourceMetricsCollector, APIManagementMetricsCollector>()

            // scanner agents
            .AddTransient<CVEAgent>()
            .AddTransient<SourceCodeAgent>();

        if (isAcaFirstPartyAgent)
        {
            // Register ACA First Party tools
            builder.RegisterAcaFirstPartyApp();
        }

        builder.Services.AddSingleton<IAgentsFactory, ThirdPartyAgentsFactory>();
        builder.Services.AddSingleton<ITitleGenerationService, TitleGenerationService>();
        builder.Services.AddSingleton<IToolsRepository, ToolsRepository>();
        builder.RegisterFunctionsFirstPartyTypes();
        builder.Services.AddTransient<IExtendedAgentService, ExtendedAgentService>();
        builder.Services.AddTransient<IExtendedAgentApiService, ExtendedAgentApiService>();
        builder.Services.AddTransient<IExtendedAgentValidator, ExtendedAgentValidator>();
        builder.Services.AddSingleton<IConnectorResolver, DataConnectorResolverService>();

        builder.Services.AddScoped<IResourceDeploymentService, ResourceDeploymentService>();

        builder.Services.AddSingleton<CustomAgentFileService>();
        builder.Services.AddSingleton<ICustomAgentFileService>(
            sp => sp.GetRequiredService<CustomAgentFileService>());

        builder.Services.AddSingleton<KustoConnector>(sp =>
        {
            var actionSettings = sp.GetRequiredService<ActionSettings>();
            var kustoAuthSetting = new ConnectorAuthSettings()
            {
                AuthenticationType = ConnectorAuthType.UAMI,
                ManagedIdentityResourceId = actionSettings.Identity ?? string.Empty,
            };
            if (builder.Environment.IsDevelopment())
            {
                kustoAuthSetting.AuthenticationType = ConnectorAuthType.User;
            }
            return new KustoConnector()
            {
                Auth = kustoAuthSetting,
                Enabled = true,
                RegionalClusterGroups = []
            };
        });
        builder.Services.AddSingleton<KustoClient>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<KustoClient>>();
            var kustoSetting = sp.GetRequiredService<KustoConnector>();
            var authSvc = sp.GetRequiredService<IAuthenticationService>();
            return new KustoClient(logger, kustoSetting, authSvc);
        });

        builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
        builder.Services.AddSingleton<IArmClientFactory, ArmClientFactory>();
        builder.Services.AddSingleton<IKubernetesClientFactory, KubernetesClientFactory>();
        builder.Services.AddKeyedSingleton<IKubernetesService, CrawlerKubernetesService>("Crawler");
        builder.Services.AddKeyedSingleton<IWatchEventService, ActivityLogService>("ActivityLog");
        builder.Services.AddKeyedSingleton<IWatchEventService, KubernetesWatchService>("Kubernetes");

        builder.Services.AddIncidentRelatedServices();
        builder.Services.AddScheduledTaskServices();

        // Register HttpClientService and configure HttpClient with proper BaseAddress
        builder.Services.AddSingleton<HttpClientService>();
        builder.Services.AddArmHelperHttpClient();
        builder.Services.AddRazorHttpClient();
        builder.Services.AddCrawlerHttpClient();
        builder.Services.AddSearchEndpointHttpClient();
        builder.Services.AddSessionPoolHttpClient();
        builder.Services.AddPagerDutyHttpClient();
        builder.Services.AddSingleton<ILogAnalyticsService, LogAnalyticsService>();
        builder.Services.AddSingleton<ILogAnalysisService, LogAnalysisService>();

        // Configure chat services
        builder.Services
            .ConfigureIChatCompletionService()
            .ConfigureAzureOpenAIClient()
            .ConfigureIChatClient(builder.Configuration)
            .ConfigureIEmbeddingGenerator(builder.Configuration);

        // Register non-keyed IEmbeddingGenerator for services that need it via constructor injection
        builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var chatClientProvider = sp.GetRequiredService<IChatClientProvider>();
            return chatClientProvider.EmbeddingModel;
        });

        // Register TrajectoryEvaluator after embedding generator is configured (it depends on IEmbeddingGenerator)
        builder.Services.AddSingleton<TrajectoryEvaluator>();

        // Register TSG Plugin - Always enabled since it uses DataConnector (not Azure Search)
        builder.Services.AddTransient<ITsgPlugin>(sp =>
        {
            return new TsgPlugin(
                sp.GetRequiredService<ILogger<TsgPlugin>>(),
                sp.GetRequiredService<DataConnectorIndex>());
        });
        builder.Services.AddTransient<TsgPluginDefinition>();

        // Register ApplensDetectorPlugin - now always enabled
        builder.Services.AddTransient<ApplensDetectorPluginDefinition>(sp =>
        {
            return new ApplensDetectorPluginDefinition(sp.GetRequiredService<IApplensDetectorPlugin>());
        });
        builder.Services.AddTransient<IApplensDetectorPlugin>(sp =>
        {
            return new ApplensDetectorPlugin(
                sp.GetRequiredService<IApplensService>(),
                sp.GetRequiredService<ILogger<ApplensDetectorPlugin>>());
        });

        // Register all SubAgent types
        foreach (var subAgentType in SubAgentDiscovery.DiscoverSubAgentTypes())
        {
            builder.Services.AddSingleton(subAgentType);
        }

        builder.Services.AddSingleton<HeartbeatReporter>();

        builder.Services.AddHostedService<TimerService>();

        // Kick off MCP Server Initializer
        builder.Services.AddSingleton<MCPMetaAgent>();
        builder.Services.AddHostedService<MCPMetaAgentManagementService>();

        // Add new MCP agent services
        builder.Services.AddMcpAgentServices();

        builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();
        builder.Services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();
        builder.Services.AddSingleton<IBot, TeamsBot>()
                        .AddSingleton<IBotPollingMessage, TeamsBot>();
        // Add the new polling service
        builder.Services.AddHostedService<TeamsMessagePollingService>();
        builder.Services.AddCosmosClient();
        ConfigureAgentMemory(builder);

        ConfigureLogger(builder);

        // Register TeamsConnector service
        builder.Services.AddSingleton<TeamsConnector>();

        // Add services to the container.
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddControllers
            (options =>
            {
                // Register YAML formatter
                options.InputFormatters.Insert(0, new YamlInputFormatter());
            })
            .AddJsonOptions(options =>
            {
                // Allow HTML in JSON responses
                options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
                // Convert enum values as strings
                options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            });

        // Feature flags
        builder.Services.AddFeatureManagement();

        // Add Blazor services
        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();

        // Add GraphService registration
        builder.Services.AddSingleton<IGraphService, GraphService>();        // Add Websocket service registration

        builder.Services.AddOpenTelemetry().WithTracing(tracingBuilder =>
        {
            tracingBuilder.AddSource("SREAgent")
                .AddSource("Agent.Customer")  // Add CustomerTracer ActivitySource
                .SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService(serviceName: "SREAgent", serviceVersion: "1.1.0"));

            // Store exported activities for in-memory access if needed
            // Register this OUTSIDE of the processor factory to avoid read-only collection issue
            var exportedActivities = new List<Activity>();
            builder.Services.AddSingleton<ICollection<Activity>>(exportedActivities);

            // Create the processor with appropriate exporter based on environment
            tracingBuilder.AddProcessor(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<AgentTraceProcessor>>();                // Create a list of exporters to use
                var exporters = new List<BaseExporter<Activity>>();

                // Configure exporter with appropriate settings based on environment
                // if (builder.Environment.IsDevelopment())
                // TODO: sanmeht - only enable traces for non-production environments
                // {
                // In-memory exporter for development - direct implementation
                exporters.Add(new InMemoryActivityExporter(exportedActivities));

                // }

                // Set up populate columns delegate
                PopulateColumnsDelegate populateColumns = (activity, trace) =>
                {
                    // Add standard fields from activity tags
                    trace["ThreadId"] = activity.GetTagItem("thread.id")?.ToString() ?? string.Empty;
                    trace["OperationName"] = activity.TagObjects.FirstOrDefault(t => t.Key == "operation.name").Value?.ToString() ?? string.Empty;
                    trace["ToolName"] = activity.TagObjects.FirstOrDefault(t => t.Key == "tool.name").Value?.ToString() ?? string.Empty;
                    trace["AgentName"] = activity.TagObjects.FirstOrDefault(t => t.Key == "agent.name").Value?.ToString() ?? string.Empty;
                    trace["ModelInputTokensCount"] = activity.TagObjects.FirstOrDefault(t => t.Key == "model.input.tokens.count").Value?.ToString() ?? "0";
                    trace["ModelOutputTokensCount"] = activity.TagObjects.FirstOrDefault(t => t.Key == "model.output.tokens.count").Value?.ToString() ?? "0";
                    trace["ModelTotalTokensCount"] = activity.TagObjects.FirstOrDefault(t => t.Key == "model.total.tokens.count").Value?.ToString() ?? "0";
                    trace["AgentId"] = AgentNameHelper.GetAgentName(builder.Environment.IsProduction());
                };

                // Add EventHub exporter if configured, otherwise use Azure Data Explorer exporter
                if (!string.IsNullOrEmpty(azureSettings?.AgentTraceEventHub.FullyQualifiedNamespace))
                {
                    var authService = sp.GetRequiredService<IAuthenticationService>();
                    var option = new EventHubTraceExporterOptions(
                        azureSettings.AgentTraceEventHub.FullyQualifiedNamespace,
                        azureSettings.AgentTraceEventHub.EventHubName,
                        populateColumns,
                        azureSettings.AgentTraceEventHub.CertificatePath,
                        azureSettings.AgentTraceEventHub.FirstPartyAppClientId,
                        azureSettings.AgentTraceEventHub.FirstPartyAppTenantId);
                    var cred = authService.GetEventHubTraceExportCredential(option);
                    exporters.Add(new EventHubTraceExporter(option, cred));
                }
                else if (builder.Environment.IsProduction() && !string.IsNullOrEmpty(azureSettings?.AgentTraceKusto.ClusterUri))
                {
                    try
                    {
                        // Create and add ADX exporter
                        exporters.Add(new AzureDataExplorerExporter(new AzureDataExplorerExporterOptions(
                            azureSettings.AgentTraceKusto.ClusterUri,
                            azureSettings.AgentTraceKusto.DatabaseName,
                            "AgentTrace",
                            populateColumns,
                            azureSettings.AgentTraceKusto.CertificatePath,
                            azureSettings.AgentTraceKusto.FirstPartyAppClientId,
                            azureSettings.AgentTraceKusto.FirstPartyAppTenantId)));

                        logger.LogInternalInformation("Successfully configured Azure Data Explorer exporter");
                    }
                    catch (Exception ex)
                    {
                        logger.LogInternalError(ex, "Failed to create Azure Data Explorer exporter, falling back to in-memory only");
                    }
                }

                // If no exporters were successfully added, add an in-memory one as fallback
                if (exporters.Count == 0)
                {
                    logger.LogInternalWarning("No exporters configured, using in-memory exporter as fallback");
                    exporters.Add(new InMemoryActivityExporter(exportedActivities));
                }

                // Create the processor with the configured exporters
                return new AgentTraceProcessor(
                    logger,
                    exporters);
            });

            // Add OutboundRequestTraceProcessor for ARM API request tracing
            tracingBuilder.AddProcessor(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<OutboundRequestTraceProcessor>>();
                var outboundExporters = new List<BaseExporter<Activity>>();

                // Set up populate columns delegate for outbound request traces
                PopulateColumnsDelegate populateOutboundRequestColumns = (activity, trace) =>
                {
                    // Add outbound request specific fields from activity tags
                    trace["HostName"] = activity.TagObjects.FirstOrDefault(t => t.Key == "server.address").Value ?? string.Empty;
                    trace["RequestUri"] = activity.TagObjects.FirstOrDefault(t => t.Key == "url.full").Value
                                     ?? activity.TagObjects.FirstOrDefault(t => t.Key == "http.url").Value ?? string.Empty;
                    trace["HttpMethod"] = activity.TagObjects.FirstOrDefault(t => t.Key == "http.request.method").Value
                                                ?? activity.TagObjects.FirstOrDefault(t => t.Key == "http.method").Value ?? string.Empty;
                    trace["HttpStatusCode"] = activity.TagObjects.FirstOrDefault(t => t.Key == "http.response.status_code").Value
                                                    ?? activity.TagObjects.FirstOrDefault(t => t.Key == "http.status_code").Value ?? string.Empty;
                    trace["DurationInMilliseconds"] = activity.Duration.TotalMilliseconds.ToString();
                    trace["AgentName"] = AgentNameHelper.GetAgentName(builder.Environment.IsProduction());
                    trace["Region"] = AgentNameHelper.GetAgentRegion(builder.Environment.IsProduction());
                    trace["RequestStartTime"] = activity.StartTimeUtc;
                    trace["PreciseTimeStamp"] = DateTime.UtcNow;
                };

                var ClusterUri = GetInternalKustoClusterConfiguration("ClusterUri");
                var DatabaseName = GetInternalKustoClusterConfiguration("DatabaseName");
                var CertificatePath = GetInternalKustoClusterConfiguration("CertificatePath");
                var FirstPartyAppClientId = GetInternalKustoClusterConfiguration("FirstPartyAppClientId");
                var FirstPartyAppTenantId = GetInternalKustoClusterConfiguration("FirstPartyAppTenantId");

                // Add Azure Data Explorer exporter if configured for production environment
                if (!string.IsNullOrEmpty(ClusterUri))
                {
                    try
                    {
                        // Create and add ADX exporter for outbound request traces
                        outboundExporters.Add(new AzureDataExplorerExporter(new AzureDataExplorerExporterOptions(
                            ClusterUri,
                            DatabaseName,
                            "AgentHttpOutgoingRequests",
                            populateOutboundRequestColumns,
                            CertificatePath,
                            FirstPartyAppClientId,
                            FirstPartyAppTenantId)));

                        logger.LogInternalInformation("Successfully configured Azure Data Explorer exporter for outbound request traces");
                    }
                    catch (Exception ex)
                    {
                        logger.LogInternalError(ex, "Failed to create Azure Data Explorer exporter for outbound request traces, using console only");
                    }
                }

                return new OutboundRequestTraceProcessor(
                    logger,
                    outboundExporters);
            });
        });
        builder.Services.AddSingleton(TracerProvider.Default.GetTracer("SREAgent"));

        // Configure OpenTelemetry Logging
        builder.Services.AddOpenTelemetry().WithLogging(loggingBuilder =>
        {
            loggingBuilder
                .SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService(serviceName: "SREAgent", serviceVersion: "1.1.0"))
                // Add a processor for Agent Action Events
                .AddProcessor(sp => CreateEventBasedLogProcessor(
                    sp.GetRequiredService<ILogger<Program>>(),
                    azureSettings,
                    adxTableName: "AgentActionEvents",
                    eventId: 1001, // EventId not used due to customPredicate
                    processorName: "AgentActionProcessor",
                    populateColumns: null,
                    customPredicate: record =>
                        (record.EventId.Id == 1001 || record.EventId.Id == 1002)
                        && (record.CategoryName?.StartsWith("Agent.", StringComparison.OrdinalIgnoreCase) == true)))

                // Add a processor to capture LLM Token Consumption logs (EventId = 2001)
                .AddProcessor(sp =>
                {
                    PopulateLogColumnsDelegate populate = (logRecord, logData) =>
                    {
                        logData["ActivityId"] = Guid.NewGuid().ToString("D");
                        try
                        {
                            var isProd = builder.Environment.IsProduction();
                            var subscriptionId = AgentNameHelper.GetSubscriptionId(isProd);
                            var resourceGroup = AgentNameHelper.GetResourceGroupName(isProd);
                            var agentName = AgentNameHelper.GetAgentName(isProd);

                            // Remove the prefix unique ID part (everything after the last double dashes)
                            var agentNameWithoutPrefix = agentName.Contains("--")
                                ? agentName.Substring(0, agentName.LastIndexOf("--"))
                                : agentName;

                            var agentResourceId = $"/subscriptions/{subscriptionId}/resourcegroups/{resourceGroup}/providers/microsoft.app/agents/{agentNameWithoutPrefix}";
                            logData["AgentResourceId"] = agentResourceId;
                        }
                        catch (Exception)
                        {
                            // best-effort: do not fail ingestion if helper throws
                            logData["AgentResourceId"] = string.Empty;
                        }
                    };

                    return CreateEventBasedLogProcessor(
                        sp.GetRequiredService<ILogger<Program>>(),
                        azureSettings,
                        adxTableName: "TokenConsumptionEvents",
                        eventId: 2001,
                        processorName: "TokenConsumptionProcessor",
                        populateColumns: populate);
                })

                // Add a processor to capture Model Request logs (EventId = 3001)
                .AddProcessor(sp => CreateEventBasedLogProcessor(
                    sp.GetRequiredService<ILogger<Program>>(),
                    azureSettings,
                    adxTableName: "ModelRequestEvents",
                    eventId: 3001,
                    processorName: "ModelRequestProcessor"))

                // Add a processor to capture ICM Request logs (EventId = 4001)
                .AddProcessor(sp => CreateEventBasedLogProcessor(
                    sp.GetRequiredService<ILogger<Program>>(),
                    azureSettings,
                    adxTableName: "IcmRequestEvents",
                    eventId: 4001,
                    processorName: "IcmRequestProcessor"))

                // Add a processor for LogInternalXX logs
                .AddProcessor(sp =>
                {
                    var ClusterUri = GetInternalKustoClusterConfiguration("ClusterUri");
                    var DatabaseName = GetInternalKustoClusterConfiguration("DatabaseName");
                    var CertificatePath = GetInternalKustoClusterConfiguration("CertificatePath");
                    var FirstPartyAppClientId = GetInternalKustoClusterConfiguration("FirstPartyAppClientId");
                    var FirstPartyAppTenantId = GetInternalKustoClusterConfiguration("FirstPartyAppTenantId");

                    BaseExporter<LogRecord>? internalLogExporter = null;

                    // Create populate columns delegate for internal logs (shared for EventHub or ADX)
                    PopulateLogColumnsDelegate populateInternalLogColumns = (logRecord, logData) =>
                    {
                        // Get the message template and attributes for formatting
                        var logMessage = logRecord.FormattedMessage ?? logRecord.Body?.ToString() ?? string.Empty;

                        // Try to format the message properly if it contains unformatted placeholders
                        if (!string.IsNullOrEmpty(logMessage) && logMessage.Contains("{") && logRecord.Attributes != null)
                        {
                            try
                            {
                                // Try to get the original template and format it
                                var messageTemplate = logMessage;

                                // Extract values from structured logging attributes
                                foreach (var kvp in logRecord.Attributes)
                                {
                                    if (kvp.Key != "{OriginalFormat}" && kvp.Key != "LogType")
                                    {
                                        // Replace placeholder in template with actual value
                                        var placeholder = "{" + kvp.Key + "}";
                                        if (messageTemplate.Contains(placeholder))
                                        {
                                            messageTemplate = messageTemplate.Replace(placeholder, kvp.Value?.ToString() ?? "null");
                                        }
                                    }
                                }
                                logMessage = messageTemplate;
                            }
                            catch (Exception)
                            {
                                // Fall back to original message if formatting fails
                            }
                        }

                        // Remove any "Internal>>>" prefix if it exists (for backward compatibility)
                        if (!string.IsNullOrEmpty(logMessage) && logMessage.Contains(">>> "))
                        {
                            var logMessageStartIndex = logMessage.IndexOf(">>>", StringComparison.Ordinal) + 4;
                            logMessage = logMessage.Substring(logMessageStartIndex);
                        }

                        // Set the cleaned message in the log data
                        logData["Message"] = logMessage;
                    };

                    // Prefer Event Hub exporter when configured; otherwise fall back to ADX if available
                    try
                    {
                        if (!string.IsNullOrEmpty(azureSettings?.AgentTraceEventHub.FullyQualifiedNamespace))
                        {
                            var ehOptions = new EventHubLogExporterOptions(
                                fullyQualifiedNamespace: azureSettings.AgentTraceEventHub.FullyQualifiedNamespace,
                                eventHubName: "sreagentdataplaneevents",
                                populateColumns: populateInternalLogColumns,
                                firstPartyAppCertificatePath: azureSettings.AgentTraceEventHub.CertificatePath,
                                firstPartyAppClientId: azureSettings.AgentTraceEventHub.FirstPartyAppClientId,
                                firstPartyAppTenantId: azureSettings.AgentTraceEventHub.FirstPartyAppTenantId);

                            internalLogExporter = new EventHubLogExporter(ehOptions);
                        }
                    }
                    catch (Exception ex)
                    {
                        var logger = sp.GetService<ILogger<Program>>();
                        logger?.LogWarning(ex, "Failed to configure EventHub exporter for InternalLogProcessor, falling back to ADX if available");
                    }

                    // If Event Hub exporter wasn't configured, create ADX exporter when ClusterUri is available
                    if (internalLogExporter == null && !string.IsNullOrEmpty(ClusterUri))
                    {
                        internalLogExporter = new AzureDataExplorerLogExporter(
                            new AzureDataExplorerLogExporterOptions(
                                clusterUri: ClusterUri,
                                databaseName: DatabaseName,
                                tableName: "SREAgentDataPlaneEvents",
                                populateColumns: populateInternalLogColumns,
                                firstPartyAppCertificatePath: CertificatePath,
                                firstPartyAppClientId: FirstPartyAppClientId,
                                firstPartyAppTenantId: FirstPartyAppTenantId));
                    }

                    return new CustomizedLogProcessor(
                        internalLogExporter,
                        record =>
                        {
                            // Process only logs from LogInternalXX methods
                            if (record.Attributes != null)
                            {
                                foreach (var kvp in record.Attributes)
                                {
                                    if (kvp.Key == "LogType" && kvp.Value?.ToString() == "Internal")
                                    {
                                        return true;
                                    }
                                }
                            }
                            return false;
                        },
                        "InternalLogProcessor");
                })

                // Add a processor for LogExternalXX logs
                .AddProcessor(sp =>
                {
                    // This processor exports external logs to customer's Kusto cluster
                    var externalClusterUri = GetKustoClusterConfiguration("ClusterUri");
                    var externalDatabaseName = GetKustoClusterConfiguration("DatabaseName");
                    var externalIdentity = GetKustoClusterConfiguration("Identity");

                    BaseExporter<LogRecord>? externalLogExporter = null;

                    // Only create the exporter if external Kusto cluster is configured
                    if (!string.IsNullOrEmpty(externalClusterUri) && !string.IsNullOrEmpty(externalDatabaseName) && !string.IsNullOrEmpty(externalIdentity))
                    {
                        // Create populate columns delegate for external logs
                        PopulateLogColumnsDelegate populateExternalLogColumns = (logRecord, logData) =>
                        {
                            // Get the message template and attributes for formatting
                            var logMessage = logRecord.FormattedMessage ?? logRecord.Body?.ToString() ?? string.Empty;

                            // Try to format the message properly if it contains unformatted placeholders
                            if (!string.IsNullOrEmpty(logMessage) && logMessage.Contains("{") && logRecord.Attributes != null)
                            {
                                try
                                {
                                    // Try to get the original template and format it
                                    var messageTemplate = logMessage;

                                    // Extract values from structured logging attributes
                                    foreach (var kvp in logRecord.Attributes)
                                    {
                                        if (kvp.Key != "{OriginalFormat}" && kvp.Key != "LogType")
                                        {
                                            // Replace placeholder in template with actual value
                                            var placeholder = "{" + kvp.Key + "}";
                                            if (messageTemplate.Contains(placeholder))
                                            {
                                                messageTemplate = messageTemplate.Replace(placeholder, kvp.Value?.ToString() ?? "null");
                                            }
                                        }
                                    }
                                    logMessage = messageTemplate;
                                }
                                catch (Exception)
                                {
                                    // Fall back to original message if formatting fails
                                }
                            }

                            // Remove any "External>>>" prefix if it exists (for backward compatibility)
                            if (!string.IsNullOrEmpty(logMessage) && logMessage.Contains(">>> "))
                            {
                                var logMessageStartIndex = logMessage.IndexOf(">>>", StringComparison.Ordinal) + 4;
                                logMessage = logMessage.Substring(logMessageStartIndex);
                            }

                            // Set the cleaned message in the log data
                            logData["Message"] = logMessage;
                        };

                        // Create the exporter with configuration for external logs table
                        // For external clusters, use managed identity authentication
                        externalLogExporter = new AzureDataExplorerLogExporter(
                            new AzureDataExplorerLogExporterOptions(
                                clusterUri: externalClusterUri,
                                databaseName: externalDatabaseName,
                                tableName: "SREAgentDataPlaneEvents",
                                populateColumns: populateExternalLogColumns,
                                firstPartyAppCertificatePath: null,
                                firstPartyAppClientId: null,
                                firstPartyAppTenantId: null,
                                identity: externalIdentity));
                    }

                    return new CustomizedLogProcessor(
                        externalLogExporter,
                        record =>
                        {
                            // Process only logs from LogExternalXX methods
                            if (record.Attributes != null)
                            {
                                foreach (var kvp in record.Attributes)
                                {
                                    if (kvp.Key == "LogType" && kvp.Value?.ToString() == "External")
                                    {
                                        return true;
                                    }
                                }
                            }
                            return false;
                        },
                        "ExternalLogProcessor");
                });
        });

        builder.AddAgentTaskService();

        return builder;
    }

    private static bool IsAcaFirstParty(string[] args)
    {
        if (Environment.GetEnvironmentVariable("AGENT_TYPE_NAME") == "IcmAgent")
        {
            return false; // IcmAgent is not a container app first-party agent
        }

        if (Environment.GetEnvironmentVariable("AGENT_TYPE_NAME") == "RCARouterAgent")
        {
            return false; // RCARouterAgent is not a container app first-party agent
        }

        if (args.Any(x => string.Equals(x.Trim(), "--is-first-party=true", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return (Environment.GetEnvironmentVariable("IS_FIRST_PARTY") ?? String.Empty).Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "y" => true,
            _ => false // Default to false if the value is invalid or not set
        };
    }

    private static bool AgentMemoryEnabled(WebApplicationBuilder builder)
    {
        var agentMemorySettings = builder.Configuration.GetSection("AppSettings:Core:AgentMemory").Get<AgentMemorySettings>();
        return agentMemorySettings?.Enabled ?? false;
    }

    private static bool SessionInsightsEnabled(WebApplicationBuilder builder)
    {
        var agentMemorySettings = builder.Configuration.GetSection("AppSettings:Core:AgentMemory").Get<AgentMemorySettings>();
        return (agentMemorySettings?.Enabled ?? false) || (agentMemorySettings?.EnableInsightPosting ?? false);
    }

    private static bool AgentMemoryRetrievalEnabled(WebApplicationBuilder builder)
    {
        var s = builder.Configuration.GetSection("AppSettings:Core:AgentMemory").Get<AgentMemorySettings>();
        if (s is null)
        {
            return false;
        }
        return s.Enabled && (s.TrajectoryRetrievalEnabled || s.DocumentRetrievalEnabled || s.UserMemoryRetrievalEnabled);
    }

    private static bool GPT5Enabled(WebApplicationBuilder builder)
    {
        var s = builder.Configuration.GetSection("AppSettings:Core:AgentModel").Get<AgentModelSettings>();
        return s?.GPT5Enabled ?? false;
    }

    private static void ConfigureAgentMemory(WebApplicationBuilder builder)
    {
        builder.Services.AddAgentMemory(AgentMemoryEnabled(builder));
    }

    private static Agent.Core.Configuration.AzureSearchSettings GetAzureSearchSettings(IConfiguration configuration)
    {
        var settings = configuration.GetSection("AppSettings:Core:Azure:AzureSearch").Get<Agent.Core.Configuration.AzureSearchSettings>();
        if (settings == null)
        {
            var firstPartySettings = configuration
                .GetSection("AppSettings:Core:External:AzureSearch")
                .Get<Agent.Core.Configuration.AzureSearchSettings>();
            if (firstPartySettings != null)
            {
                settings = firstPartySettings;
            }
            else
            {
                settings = new Agent.Core.Configuration.AzureSearchSettings();
            }
        }
        return settings;
    }

    private static void ConfigureLogger(WebApplicationBuilder builder)
    {
        builder.Services.AddLogging();

        builder.Logging.ClearProviders();

        if (builder.Environment.IsDevelopment())
        {
            builder.Logging.AddConsole();

            // Add file logging if enabled in configuration
            var loggingSettings = builder.Configuration.GetSection("Logging").Get<LoggingSettings>();
            if (loggingSettings?.EnableFileLogging == true)
            {
                // Parse rolling interval from string
                var rollingInterval = Enum.TryParse<RollingInterval>(loggingSettings.RollingInterval, true, out var interval)
                    ? interval
                    : RollingInterval.Day;

                // Create a separate Serilog logger for file output only
                var fileLogger = new LoggerConfiguration()
                    .WriteTo.File(
                        path: loggingSettings.LogFilePath,
                        rollingInterval: rollingInterval,
                        retainedFileCountLimit: loggingSettings.RetainedFileCountLimit,
                        fileSizeLimitBytes: loggingSettings.FileSizeLimitMB * 1024 * 1024,
                        shared: true,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .MinimumLevel.Information()
                    .CreateLogger();

                // Add Serilog as an additional provider, not replacing the existing ones
                builder.Logging.AddSerilog(fileLogger, dispose: true);
            }
        }

        // ConfigureKustoLoggers(builder);
        ConfigureApplicationInsightsLoggers(builder);
        // ConfigureCustomerTracer(builder);
    }

    private static void ConfigureKustoLoggers(WebApplicationBuilder builder)
    {
        const string agentLogTable = "SREAgentDataPlaneEvents";

        // The SRE Agent Kusto for Agent Logs
        var internalKustoClusterSettings = new KustoClusterConfiguration
        {
            ClusterUri = GetInternalKustoClusterConfiguration("ClusterUri"),
            DatabaseName = GetInternalKustoClusterConfiguration("DatabaseName"),
            CertificatePath = GetInternalKustoClusterConfiguration("CertificatePath"),
            FirstPartyAppClientId = GetInternalKustoClusterConfiguration("FirstPartyAppClientId"),
            FirstPartyAppTenantId = GetInternalKustoClusterConfiguration("FirstPartyAppTenantId"),
        };

        // The 1P BYO Kusto Cluster for Agent Logs
        var externalKustoClusterUri = GetKustoClusterConfiguration("ClusterUri");
        var externalKustoClusterDatabaseName = GetKustoClusterConfiguration("DatabaseName");
        var externalKustoClusterIdentity = GetKustoClusterConfiguration("Identity");

        var externalKustoClusterSettings = (!string.IsNullOrEmpty(externalKustoClusterUri)
            && !string.IsNullOrEmpty(externalKustoClusterDatabaseName)
            && !string.IsNullOrEmpty(externalKustoClusterIdentity))
            ? new KustoClusterConfiguration
            {
                ClusterUri = externalKustoClusterUri,
                DatabaseName = externalKustoClusterDatabaseName,
                Identity = externalKustoClusterIdentity
            }
            : null;

        if (!string.IsNullOrEmpty(internalKustoClusterSettings.ClusterUri))
        {
            CommonColumn commonColumn = CommonColumn.Build();

            var logger = new AzureDataExplorerLoggerProvider(
                commonColumn: commonColumn,
                internalKustoClusterUri: internalKustoClusterSettings.ClusterUri,
                internalKustoDatabaseName: internalKustoClusterSettings.DatabaseName,
                internalKustoTableName: agentLogTable,
                externalKustoClusterUri: externalKustoClusterSettings?.ClusterUri,
                externalKustoDatabaseName: externalKustoClusterSettings?.DatabaseName,
                externalKustoTableName: agentLogTable,
                externalKustoIdentityClientId: externalKustoClusterSettings?.Identity,
                kustoFirstPartyAppClientId: internalKustoClusterSettings.FirstPartyAppClientId,
                kustoFirstPartyAppTenantId: internalKustoClusterSettings.FirstPartyAppTenantId,
                kustoFirstPartyAppCertificatePath: internalKustoClusterSettings.CertificatePath);

            builder.Services.AddSingleton<ILoggerProvider>(logger);
            builder.Services.AddSingleton<AzureDataExplorerLogger>(logger.GetLogger());
        }
        else
        {
            builder.Services.AddSingleton<AzureDataExplorerLogger>(new AzureDataExplorerLogger());
        }
    }

    private static void ConfigureApplicationInsightsLoggers(WebApplicationBuilder builder)
    {
        var appInsightsConnectionString = GetApplicationInsightsConnectionString(builder);
        var customerLogger = new CustomerLogger(appInsightsConnectionString);
        var customerAuditLogger = new CustomerAuditLogger(appInsightsConnectionString);

        var processorOptions = new CustomerTraceProcessorOptions
        {
            LogFilteringActions = false, // Set to true for debugging
            MinimumDurationThreshold = TimeSpan.FromMilliseconds(100), // Filter short operations
            IncludeSystemSpans = false // Exclude system-level spans
        };

        builder.Services.AddSingleton<CustomerLogger>(customerLogger);
        builder.Services.AddSingleton<CustomerAuditLogger>(customerAuditLogger);

        builder.Services.AddOpenTelemetry().WithTracing(tracingBuilder =>
        {
            tracingBuilder
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("AgentService"))
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource("Agent.Customer")
                .AddProcessor(customerLogger.InitializeCustomerTracing(processorOptions))
                .AddProcessor(new CustomerAuditTraceFilteringProcessor(customerAuditLogger));

            if (builder.Environment.IsDevelopment())
            {
                // local
                //tracingBuilder.AddConsoleExporter();
            }
        });
    }

    private static string GetKustoClusterConfiguration(string key)
    {
        const string prefix = "AppSettings__Core__Azure__FirstParty__KustoClusterConfiguration__";
        return Environment.GetEnvironmentVariable($"{prefix}{key}") ?? string.Empty;
    }

    private static string GetInternalKustoClusterConfiguration(string key)
    {
        const string prefix = "AppSettings__Core__Azure__AgentLogKusto__";
        return Environment.GetEnvironmentVariable($"{prefix}{key}") ?? string.Empty;
    }

    /// <summary>
    /// Creates a CustomizedLogProcessor with EventHub and ADX fallback for event-based log filtering.
    /// EventHub name is automatically derived from the ADX table name by converting to lowercase.
    /// </summary>
    private static CustomizedLogProcessor CreateEventBasedLogProcessor(
        ILogger logger,
        AzureSettings? azureSettings,
        string adxTableName,
        int eventId,
        string processorName,
        PopulateLogColumnsDelegate? populateColumns = null,
        Func<LogRecord, bool>? customPredicate = null)
    {
        try
        {
            // Default predicate: filter by EventId
            var predicate = customPredicate ?? (record => record.EventId.Id == eventId);

            // Derive EventHub name from ADX table name (lowercase)
            var eventHubName = adxTableName.ToLowerInvariant();

            // Prefer Event Hub exporter when configured
            if (!string.IsNullOrEmpty(azureSettings?.AgentTraceEventHub.FullyQualifiedNamespace))
            {
                var ehOptions = new EventHubLogExporterOptions(
                    fullyQualifiedNamespace: azureSettings.AgentTraceEventHub.FullyQualifiedNamespace,
                    eventHubName: eventHubName,
                    populateColumns: populateColumns,
                    firstPartyAppCertificatePath: azureSettings.AgentTraceEventHub.CertificatePath,
                    firstPartyAppClientId: azureSettings.AgentTraceEventHub.FirstPartyAppClientId,
                    firstPartyAppTenantId: azureSettings.AgentTraceEventHub.FirstPartyAppTenantId);

                var exporter = new EventHubLogExporter(ehOptions);
                return new CustomizedLogProcessor(exporter, predicate, processorName);
            }

            // Fallback to Azure Data Explorer when ADX cluster is available
            var clusterUri = GetInternalKustoClusterConfiguration("ClusterUri");
            var databaseName = GetInternalKustoClusterConfiguration("DatabaseName");
            var certificatePath = GetInternalKustoClusterConfiguration("CertificatePath");
            var firstPartyAppClientId = GetInternalKustoClusterConfiguration("FirstPartyAppClientId");
            var firstPartyAppTenantId = GetInternalKustoClusterConfiguration("FirstPartyAppTenantId");

            if (!string.IsNullOrEmpty(clusterUri))
            {
                var options = new AzureDataExplorerLogExporterOptions(
                    clusterUri: clusterUri,
                    databaseName: databaseName,
                    tableName: adxTableName,
                    populateColumns: populateColumns,
                    firstPartyAppCertificatePath: certificatePath,
                    firstPartyAppClientId: firstPartyAppClientId,
                    firstPartyAppTenantId: firstPartyAppTenantId);

                var exporter = new AzureDataExplorerLogExporter(options);
                return new CustomizedLogProcessor(exporter, predicate, processorName);
            }

            // No exporter configured -> no-op processor
            return new CustomizedLogProcessor(null, record => false, processorName);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to configure {ProcessorName} exporter, falling back to no-op processor", processorName);
            return new CustomizedLogProcessor(null, record => false, processorName);
        }
    }

    private static string GetApplicationInsightsConnectionString(WebApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment())
        {
            // Get Azure Settings for App Insights configuration
            var azureSettings = builder.Configuration.GetSection("AppSettings:Core:Azure").Get<AzureSettings>();
            var loggingSettings = builder.Configuration.GetSection("Logging").Get<LoggingSettings>();
            var connString = azureSettings?.AppInsights?.ConnectionString;
            return connString ?? string.Empty;
        }

        return Environment.GetEnvironmentVariable("AppSettings__Core__Azure__ApplicationInsights__ConnectionString") ?? string.Empty;
    }

    // Helper method to get Azure Portal domains
    private static string[] GetAzurePortalDomains(IConfiguration configuration)
    {
        var azurePortalDomains = "";
        var configDomains = configuration.GetValue<string>("AppSettings:AzurePortalDomains");
        if (!string.IsNullOrEmpty(configDomains))
        {
            azurePortalDomains = configDomains;
        }

        return azurePortalDomains.Split(',');
    }

    /// <summary>
    /// Validates that required AgentMemory configuration settings are present
    /// </summary>
    /// <param name="settings">The AgentMemorySettings to validate</param>
    /// <returns>True if configuration is valid, false otherwise</returns>
    private static bool IsAgentMemoryConfigurationValid(AgentMemorySettings settings)
    {
        var storageAccountValid = !settings.StorageAccountEnabled
            ||
            (!string.IsNullOrEmpty(settings.StorageAccountName)
            && !string.IsNullOrEmpty(settings.BlobStorageContainerName)
            && !string.IsNullOrEmpty(settings.BlobStorageResourceId)
            && !string.IsNullOrEmpty(settings.AzureAISearchIndexerName)
            && !string.IsNullOrEmpty(settings.AzureAISearchSkillSetName)
            && !string.IsNullOrEmpty(settings.AzureAISearchDataSourceName));

        return storageAccountValid
            && !string.IsNullOrEmpty(settings.AzureAISearchName)
            && !string.IsNullOrEmpty(settings.AzureAISearchIndexName)
            && !string.IsNullOrEmpty(settings.ManagedIdentityResourceId);
    }

    private static bool ShouldForceEnableSkills(WebApplicationBuilder builder)
    {
        // TODO: this is a temporary way to enable skills for e2e testing of 3p skills w/o changing default agent configs

        var agentName = AgentNameHelper.GetCustomerAgentName(isProd: builder.Environment.IsProduction());
        var configSet = builder.Configuration.GetValue<bool>("Enable3PSkills");

        return agentName.Contains("enable-3p-skills", StringComparison.InvariantCultureIgnoreCase) || configSet;
    }
}

