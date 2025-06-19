// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using Agent.Core.Configuration;
using Agent.Core.Extensions;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Plugins.Definitions;
using Agent.Core.Services;
using Agent.Core.Services.TokenService;
using Agent.Data;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Graph.Crawler;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Crawler.Metrics;
using Agent.Graph.Interfaces;
using Agent.Graph.Services;
using Agent.Logging;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Plugins.Implementation;
using Agent.Plugins.Implementation.DiagnosticsPlugin;
using Agent.Plugins.Interface;
using Agent.Plugins.Services;
using Agent.Plugins.Services.Interfaces;
using Agent.Prometheus.Services;
using Kusto.Data;
using Kusto.Ingest;
using Agent.Runtime;
using Agent.Runtime.Clients.Search;
using Agent.Runtime.Communication;
using Agent.Runtime.HelperAgents;
using Agent.Runtime.IncidentHandlerAgent;
using Agent.Runtime.Indexing.Documentation;
using Agent.Runtime.Interfaces;
using Agent.Runtime.MetaAgent;
using Agent.Runtime.MetaAgent.Interfaces;
using Agent.Runtime.MetaAgent.SubAgentPlugins;
using Agent.Runtime.Reasoning;
using Agent.Runtime.Services;
using Agent.Runtime.Services.AzMonitorAlertInvestigation;
using Agent.Runtime.Services.AzMonitorAlertInvestigationService;
using Agent.Runtime.SubAgents;
using Agent.Runtime.SubAgents.AppCodeAnalysisAgent;
using Agent.Runtime.SubAgents.AppReliabilityAgent;
//using Agent.Runtime.SubAgents.AppServiceRemediation;
using Agent.Runtime.SubAgents.AzMonitorAlertAgent;
using Agent.Runtime.SubAgents.ContainerAppsRemediation;
using Agent.Runtime.SubAgents.Core;
using Agent.Runtime.SubAgents.CPUAnalysisAgent;
using Agent.Runtime.SubAgents.CVEAgent;
using Agent.Runtime.SubAgents.DailyReportSummary;
using Agent.Runtime.SubAgents.FeedbackRCAAgent;
using Agent.Runtime.SubAgents.FunctionAppConfigurationCheck;
using Agent.Runtime.SubAgents.FunctionAppConnectivityAgent;
using Agent.Runtime.SubAgents.FunctionAppDeploymentChecksAgent;
using Agent.Runtime.SubAgents.FunctionAppDiagnosticsAgent;
using Agent.Runtime.SubAgents.FunctionAppExecutionFailuresAgent;
using Agent.Runtime.SubAgents.IcmScanner;
using Agent.Runtime.SubAgents.KubernetesAgent;
using Agent.Runtime.SubAgents.ManagedIdentityMigration;
using Agent.Runtime.SubAgents.PagerDutyAgent;
using Agent.Runtime.SubAgents.SourceCodeAgent;
using Agent.Runtime.SubAgents.SqlDbQueryPerfAgent;
using Agent.Runtime.SubAgents.TlsBestPractices;
using Agent.Runtime.SubAgents.TlsBestPracticesAgent;
using Agent.Runtime.SubAgents.VmRdpInvestigatorAgent;
using Agent.Runtime.SubAgents.WebAppDownAgent;
using Agent.Runtime.TeamsChatServices;
using Agent.Web.Services;
using Azure.Monitor.OpenTelemetry.Exporter;
using FirstPartyAgent.Core.FirstPartyAgents;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.AzureManaged;
using Microsoft.DurableTask.Worker;
using Microsoft.DurableTask.Worker.AzureManaged;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Agent.Web;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = CreateWebApplicationBuilder(args);

        var app = builder.Build();

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
        app.UseCors(x => x.WithOrigins(GetAzurePortalDomains(builder.Configuration))
                          .AllowAnyHeader()
                          .AllowCredentials()
                          .SetIsOriginAllowedToAllowWildcardSubdomains());

        app.UseHttpsRedirection();
        // Serve static files from wwwroot
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseRouting();

        app.MapControllers();
        app.MapBlazorHub();

        // Map SignalR hub
        app.MapHub<Agent.Web.SignalR.AgentHub>("/agentHub");

        // Finally, map the fallback page
        app.MapFallbackToFile("/static/index.html");

        var azureSettings = builder.Configuration.GetSection("AppSettings:Core:Azure").Get<AzureSettings>();
        var loggingSettings = builder.Configuration.GetSection("Logging").Get<LoggingSettings>();

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            _ = Task.Run(async () =>
            {
                await app.Services.CreateCosmosContainerIfNotExists(builder.Configuration);
            });
        });

        app.Run();

        ResourceBuilder resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddService(serviceName: builder.Environment.ApplicationName, serviceVersion: "0.0.1")
            .AddAttributes(new Dictionary<string, object>
            {
                ["environment.name"] = builder.Environment.EnvironmentName,
                ["team.name"] = "backend"
            });

        using TracerProvider tracerProvider = GetTracerProvider(resourceBuilder, azureSettings, loggingSettings);
        using MeterProvider meterProvider = GetMeterProvider(resourceBuilder, azureSettings);
    }

    public static WebApplicationBuilder CreateWebApplicationBuilder(string[] args)
    {
        return CreatePreliminaryWebApplicationBuilder(args);
    }

    public static WebApplicationBuilder CreatePreliminaryWebApplicationBuilder(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var isFirstAgent = (Environment.GetEnvironmentVariable("IS_FIRST_PARTY") ?? String.Empty).Trim().ToLower() switch
        {
            "true" or "1" or "y" => true,
            "false" or "0" or "n" => false,
            _ => false // Default to false if the value is invalid or not set
        };

        builder.LoadAppSettings(builder.Environment.IsDevelopment());
        builder.ValidateAndRegisterAppSettings<AppSettings>();

        // Configure Azure settings
        builder.Services.Configure<AzureSettings>(
            builder.Configuration.GetSection("Azure"));

        builder.Services.AddLogging();

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

        // Configure Azure Search Settings settings
        builder.Services.Configure<SearchSettings>(
            builder.Configuration.GetSection("AppSettings:Core:SearchOptions"));

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

        // Register a default ConversationReference that can be injected into PostToTeamsPlugin
        // builder.Services.AddSingleton<Microsoft.Bot.Schema.ConversationReference>(new Microsoft.Bot.Schema.ConversationReference());

        // Register plugins and their dependencies

        builder.Services
            .AddSingleton<Agent.Runtime.MetaAgent.IAgent, MetaAgent>()
            .AddSingleton<IIncidentHandlerAgent, IncidentHandlerAgent>()
            .AddSingleton<IAppServicePlugin, AppServicePlugin>()
            .AddSingleton<AppServicePluginDefinition>()
            .AddSingleton<IFunctionAppsPlugin, FunctionAppsPlugin>()
            .AddSingleton<FunctionAppsPluginDefinition>()
            .AddSingleton<IGraphDatabaseClient, GremlinGraphDatabaseClient>()

            .AddSingleton<ITimePlugin, TimePlugin>()
            .AddSingleton<IMetricsPlugin, MetricsPlugin>()
            .AddSingleton<IAppInsightsPlugin, AppInsightsPlugin>()
            .AddSingleton<AppInsightsPluginDefinition>()
            .AddSingleton<Agent.Plugins.Models.GitHubClient>()
            .AddTransient<IGithubIssuePlugin, GitHubIssuePlugin>()
            .AddSingleton<IRemediationPlugin, RemediationPlugin>()
            .AddSingleton<AzureResourceGraphClient>()
            .AddSingleton<ArmHelper>()
            .AddSingleton<AzureMonitorMetricsHelper>()
            .AddSingleton<ArmResourceCrawlerFactory>()
            .AddSingleton<ICrawlerService, ResourceGraphCrawlerService>()
            .AddSingleton<ICrawlerTriggerService, CrawlerTriggerService>()
            .AddSingleton<IReliabilityPlugin, ReliabilityPlugin>()
            .AddTransient<IMetaAgentAppReliabilityPlugin, AppReliabilityPlugin>()
            .AddSingleton<AppReliabilityAgentFactory>()
            .AddSingleton<AppCodeAnalysisAgentFactory>()
            .AddSingleton<INSGRulePlugin, NSGRulePlugin>()
            .AddSingleton<ContainerAppsRemediationAgentFactory>()
            .AddSingleton<IContainerAppPlugin, ContainerAppPlugin>()
            .AddSingleton<IRemoteWriteService, RemoteWriteService>()
            .AddSingleton<AzureSupportCenterHelper>()
            .AddSingleton<IAzureSupportCenterPlugin, AzureSupportCenterPlugin>()
            .AddSingleton<VmRdpInvestigatorAgentFactory>()
            .AddTransient<IMetaAgentVmRdpInvestigatorPlugin, VmRdpInvestigatorPlugin>()
            .AddSingleton<AppInsightsSettings>()
            .AddSingleton<FunctionAppConnectivityAgentFactory>()
            .AddTransient<IMetaAgentFunctionAppConnectivityPlugin, FunctionAppConnectivityPlugin>()
            .AddSingleton<FunctionAppExecutionFailuresAgentFactory>()
            .AddTransient<IMetaAgentFunctionAppExecutionFailuresAgentPlugin, FunctionAppExecutionFailuresAgentPlugin>()
            .AddSingleton<IPrometheusQueryService, PrometheusQueryService>()
            .AddSingleton<IRoleAssignmentPlugin, RoleAssignmentPlugin>()

            .AddSingleton<SqlDbQueryPerfAgentFactory>()
            .AddTransient<IMetaAgentSqlDbQueryPerfPlugin, SqlDbQueryPerfPlugin>()

            .AddTransient<IMetaAgentFunctionAppDiagnosticsPlugin, FunctionAppDiagnosticsPlugin>()
            .AddSingleton<FunctionAppDiagnosticsAgentFactory>()

            .AddSingleton<FunctionAppConfigurationCheckAgentFactory>()
            .AddTransient<IFunctionAppConfigurationChecksPlugin, FunctionAppConfigurationChecksPlugin>()
            .AddTransient<IMetaAgentFunctionAppConfigurationCheckAgentPlugin, FunctionAppConfigurationCheckPlugin>()

            .AddSingleton<FunctionAppDeploymentChecksAgentFactory>()
            .AddTransient<IFunctionAppDeploymentChecksPlugin, FunctionAppDeploymentChecksPlugin>()
            .AddTransient<IMetaAgentFunctionAppDeploymentChecksAgentPlugin, FunctionAppDeploymentChecksAgentPlugin>()

            .AddTransient<MetricsPluginDefinition>()
            .AddTransient<AzureMonitorMetricsPluginDefinition>()
            .AddTransient<ChartPluginDefinition>()
            .AddTransient<RecordActionsPluginDefinition>()
            .AddTransient<GrafanaPluginDefinition>()
            .AddTransient<GraphDBPluginDefinition>()
            .AddTransient<ArmPluginDefinition>()
            .AddTransient<TimePluginDefinition>()
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
            .AddTransient<RoleAssignmentPluginDefinition>()
            .AddTransient<PagerDutyIncidentPluginDefinition>()
            .AddTransient<FunctionAppExecutionFailuresPluginDefinition>()
            .AddTransient<FunctionAppConfigurationChecksPluginDefinition>()
            .AddTransient<FunctionAppDeploymentChecksPluginDefinition>()
            .AddTransient<UserInteractionPluginDefinition>()
            .AddTransient<AgentControlFlowPluginDefinition>()
            .AddTransient<APIManagementPluginDefinition>()
            .AddTransient<RCAContainerAppsIngressPluginDefinition>()
            .AddTransient<RCAContainerAppCorednsPluginDefinition>()
            .AddTransient<RCAContainerAppOutboundConnectionPluginDefinition>()
            .AddTransient<RCAContainerAppsManagedEnvironmentPluginDefinition>()
            .AddTransient<RCAContainerAppsManagedClusterPluginDefinition>()
            .AddTransient<RCAContainerAppsJobsPluginDefinition>()
            .AddTransient<RCAContainerAppsSessionsPluginDefinition>()
            .AddTransient<RCAContainerAppCustomerLogsPluginDefinition>()
            .AddTransient<RCAContainerAppIcMPluginDefinition>()
            .AddTransient<RCAContainerAppCustomerMetricsPluginDefinition>()
            .AddTransient<RCAContainerAppQuotaPluginDefinition>()
            .AddTransient<RCAContainerAppRevisionPluginDefinition>()
            .AddSingleton<IKustoDashboardPlugin, KustoDashboardPlugin>()
            .AddTransient<RCAContainerAppResourceCheckPluginDefinition>()
            .AddTransient<GenevaActionsPluginDefinition>()

            .AddTransient<IMetaAgentContainerAppsRemediationPlugin, ContainerAppsRemediationPlugin>()
            .AddTransient<IMetaAgentManagedIdentityMigrationPlugin, ManagedIdentityMigrationPlugin>()
            .AddTransient<IMetaAgentTlsBestPracticesPlugin, TlsBestPracticesPlugin>()
            .AddTransient<IMetaAgentKubernetesAgentPlugin, KubernetesAgentPlugin>()
            .AddTransient<IMetaAgentAksQaAgentPlugin, AksQaAgentPlugin>()
            .AddTransient<IMetaAgentWebAppDownPlugin, WebAppDownPlugin>()
            .AddTransient<IMetaAgentCPUAnalysisPlugin, CPUAnalysisAgentPlugin>()
            .AddTransient<IMetaAgentAppCodeAnalysisPlugin, AppCodeAnalysisAgentPlugin>()
            .AddTransient<IKubePlugin, KubePlugin>()
            //.AddTransient<IMetaAgentAppServiceRemediationPlugin, AppServiceRemediationPlugin>()
            .AddTransient<IChartPlugin, ChartPluginV2>()
            .AddTransient<ChartPluginV2>()
            .AddTransient<IGraphDBPlugin, GraphDBPlugin>()
            .AddTransient<IPagerDutyIncidentPlugin, PagerDutyIncidentPlugin>()
            .AddTransient<IFunctionAppExecutionFailuresPlugin, FunctionAppExecutionFailuresPlugin>()
            .AddTransient<IAzureMonitorMetricsPlugin, AzureMonitorMetricsPlugin>()
            .AddTransient<IArmPlugin, ArmPlugin>()
            .AddTransient<IAPIManagementPlugin, APIManagementPlugin>()
            .AddTransient<IGenevaActionsPlugin, GenevaActionsPlugin>()

            //.AddSingleton<AppServiceRemediationAgentFactory>()
            .AddSingleton<KubernetesAgentFactory>()
            .AddSingleton<AksQaAgentFactory>()
            .AddSingleton<ManagedIdentityMigrationAgentFactory>()
            .AddSingleton<TlsBestPracticeAgentFactory>()
            .AddSingleton<TlsBestPracticesScanner>()
            .AddTransient<IMetaAgentLocalAuthPlugin, LocalAuthAgentPlugin>()
            .AddSingleton<WebAppDownAgentFactory>()
            .AddSingleton<CPUAnalysisAgentFactory>()
            .AddSingleton<AppCodeAnalysisAgentFactory>()
            .AddSingleton<SourceCodeScanner>()
            .AddSingleton<CVEScanner>()
            .AddSingleton<FeedbackRCAScanner>()
            .AddSingleton<IAzMonitorAlertService, AzMonitorAlertService>()
            .AddSingleton<ILogQueryService, LogQueryService>()
            .AddSingleton<IAzMonitorAlertInvestigationService, AzMonitorAlertInvestigationService>()
            .AddSingleton<AzMonitorAlertScanner>()
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
            .AddSingleton<DailyReportSummaryAgentFactory>()
            .AddSingleton<IPostToTeamsPlugin, PostToTeamsPlugin>()
            .AddSingleton<IConnectedIntegrationsPlugin, ConnectedIntegrationsPlugin>()
            .AddSingleton<IGrafanaPlugin, GrafanaPlugin>()
            .AddSingleton<IRecordActionsPlugin, RecordActionsPlugin>()
            .AddSingleton<IGithubWorkflowTriggerPlugin, GithubWorkflowTriggerPlugin>()
            .AddSingleton<IMIConfigurationCheckPlugin, MIConfigurationCheckPlugin>()
            .AddSingleton<IAppIdentityUpdatePlugin, AppIdentityUpdatePlugin>()
            .AddSingleton<ITimePlugin, TimePlugin>()
            .AddSingleton<McpToolsRepository>()
            .AddSingleton<IThreadOrchestrationManager, CosmosThreadOrchestrationManager>()
            .AddSingleton<SinkService>()
            .AddSingleton<ThreadService>()
            .AddSingleton<ThreadManagementService>()
            .AddSingleton<IAgentInboundCommunicationService, InboundCommunicationService>()
            .AddSingleton<IStreamingService, Agent.Web.Services.SignalRStreamingService>()
            .AddSingleton<IAgentOutboundCommunicationService, OutboundCommunicationService>()
            .AddSingleton<IApprovalService, ApprovalService>()
            .AddSingleton<IRemoteWriteService, RemoteWriteService>()
            .AddSingleton<IMetricsRegistry, MetricsRegistry>()
            .AddSingleton<IGremlinMetricsService, GremlinMetricsService>()
            .AddSingleton<AppInsightsPlugin>()
            .AddTransient<ICpuAnalysisPlugin, CpuAnalysisPlugin>()
            .AddTransient<IAppCodeAnalysisPlugin, AppCodeAnalysisPlugin>()
            .AddSingleton<IReasoningLoopFactory, ReasoningLoopFactory>()
            .AddSingleton<IReasoningLoopManager, ReasoningLoopManager>()
            .AddSingleton<ISearchPlugin, SearchPlugin>()
            .AddSingleton<ISearchIndexingClient, SearchIndexingClient>()
            .AddSingleton<DocumentationIndex>()

            .AddSingleton(sp =>
            {
                return new ToolFactory<AgentContext>(
                    logger: sp.GetRequiredService<ILogger<ToolFactory<AgentContext>>>(),
                    serviceProvider: sp,
                    assembliesToScan: AppDomain.CurrentDomain.GetAssemblies()
                        .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                        .Where(assembly => assembly.GetName()?.Name?.StartsWith("Agent.") == true));
            })
            .AddSingleton<IToolFactory<AgentContext>, ToolFactory<AgentContext>>(sp =>
            {
                return sp.GetRequiredService<ToolFactory<AgentContext>>();
            })

            .AddSingleton<IAgentFactory<AgentContext>, AgentFactory<AgentContext>>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var azureSettings = configuration.GetSection("AppSettings:Core:Azure").Get<AzureSettings>();
                var modeConfigurator = sp.GetRequiredService<IAgentModeConfigurator<AgentContext>>();

                return new AgentFactory<AgentContext>(
                    logger: sp.GetRequiredService<ILogger<AgentFactory<AgentContext>>>(),
                    toolFactory: sp.GetRequiredService<IToolFactory<AgentContext>>(),
                    assembliesToScan: AppDomain.CurrentDomain.GetAssemblies()
                        .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                        .Where(assembly => assembly.GetName()?.Name?.StartsWith("Agent.Runtime") == true),
                    modeConfigurator: modeConfigurator,
                    agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "AgentsV2"),
                    commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "CommonPrompts"),
                    promptStarters: [Core.Constants.SREAgentPromptStarter],
                    promptEnders: [Core.Constants.SREAgentFinalInstructions],
                    defaultOutputType: typeof(DefaultAgentOutput)
                );
            })
            .AddSingleton<IDiagnosticsPlugin, DiagnosticsPlugin>()
            .AddSingleton<IMetaAgentFunctionAppDiagnosticsPlugin, FunctionAppDiagnosticsPlugin>()
            .AddSingleton<ISearchPlugin, SearchPlugin>()

            // Register the communication activities
            .AddSingleton<UpdateThreadWithAgentMessageActivity>()
            .AddSingleton<NotifyCompletionActivity>()
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
            .AddSingleton<IResourceMetricsCollector, ContainerAppMetricsCollector>()
            .AddSingleton<IResourceMetricsCollector, FunctionAppMetricsCollector>()
            .AddSingleton<IResourceMetricsCollector, AppServiceMetricsCollector>()
            .AddSingleton<IResourceMetricsCollector, RedisMetricsCollector>()
            .AddSingleton<IResourceMetricsCollector, AKSMetricsCollector>()

            // helper agents
            .AddTransient<HelperAgentsPluginDefinition>()
            .AddTransient<DiagnosisAgent>()

            // scanner agents
            .AddTransient<CVEAgent>()
            .AddTransient<SourceCodeAgent>()
            ;

        if (isFirstAgent)
        {
            builder.Services.AddSingleton<IAgentsFactory, FirstPartyAgentsFactory>();
            builder.Services.AddSingleton<IToolsRepository, FirstPartyToolsRepository>();
            builder.Services.AddSingleton<ITitleGenerationService, FirstPartyTitleGenerationService>();
            builder.RegisterFirstPartySubAgentsDependencies();
            builder.RegisterFirstPartyAppSettings();
        }
        else
        {
            builder.Services.AddSingleton<IAgentsFactory, ThirdPartyAgentsFactory>();
            builder.Services.AddSingleton<IToolsRepository, ToolsRepository>();
            builder.Services.AddSingleton<ITitleGenerationService, TitleGenerationService>();
        }

        builder.ValidateAndRegisterFirstPartyTypes();
        // Register all subagent factories that derive from the shared impl
        var genericSubAgentFactories = TypeReflectionHelpers.GetClassesDerivedFromGeneric(typeof(MetaAgent).Assembly, typeof(SimpleResourceSubAgentFactoryBase<,,,>));
        foreach (var type in genericSubAgentFactories)
        {
            builder.Services.AddSingleton(type);
        }
        // Register all subagent plugins that derive from the shared impl
        var genericSubAgentPlugins = TypeReflectionHelpers.GetClassesDerivedFromGeneric(typeof(MetaAgent).Assembly, typeof(SimpleResourceSubAgentPluginBase<,,,,>));
        foreach (var type in genericSubAgentPlugins)
        {
            builder.Services.AddTransient(type);
        }
        // Register all subagent scanners that derive from the shared impl
        var genericSubAgentScanners = TypeReflectionHelpers.GetClassesDerivedFromGeneric(typeof(MetaAgent).Assembly, typeof(SimpleResourceSubAgentScannerBase<,,,>));
        foreach (var type in genericSubAgentScanners)
        {
            builder.Services.AddSingleton(type);
        }

        builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
        builder.Services.AddSingleton<IArmClientFactory, ArmClientFactory>();
        builder.Services.AddSingleton<IKubernetesClientFactory, KubernetesClientFactory>();
        builder.Services.AddKeyedSingleton<IKubernetesService, CrawlerKubernetesService>("Crawler");
        builder.Services.AddKeyedSingleton<IWatchEventService, ActivityLogService>("ActivityLog");
        builder.Services.AddKeyedSingleton<IWatchEventService, KubernetesWatchService>("Kubernetes");


        var serviceProvider = builder.Services.BuildServiceProvider();
        var incidentManagementSettings = serviceProvider.GetRequiredService<IncidentManagementSettings>();

        switch (incidentManagementSettings.Type)
        {
            case IncidentManagementType.PagerDuty:
                builder.Services.AddSingleton<IPagerDutyService, PagerDutyService>();
                builder.Services.AddSingleton<IICMAPIClient, NullableICMAPIClient>();
                builder.Services.AddSingleton<IIncidentScanner, PagerDutyScanner>();
                break;
            case IncidentManagementType.Icm:
                builder.Services.AddSingleton<IPagerDutyService, NullablePagerDutyService>();
                builder.Services.AddSingleton<IICMAPIClient, ICMAPIClient>();
                builder.Services.AddSingleton<IIncidentScanner, IcmScanner>();

                var logger = serviceProvider.GetRequiredService<ILogger<ICMAPITokenService>>();
                var actionSettings = serviceProvider.GetRequiredService<ActionSettings>();
                ICMAPITokenService.Instance.Initialize(actionSettings, incidentManagementSettings.ICMAPI, logger);
                break;
            default:
                builder.Services.AddSingleton<IPagerDutyService, NullablePagerDutyService>();
                builder.Services.AddSingleton<IICMAPIClient, NullableICMAPIClient>();
                builder.Services.AddSingleton<IIncidentScanner, NullableIncidentScanner>();
                break;
        }

        //Todo, add generic interface/class for PagerDutyIncidentDocument/IcmDocument and dynamically register
        builder.Services.AddSingleton<IIncidentManagementService<PagerDutyIncidentDocument>, IncidentManagementService<PagerDutyIncidentDocument>>();
        builder.Services.AddSingleton<IIncidentManagementService<IcmIncidentDocument>, IncidentManagementService<IcmIncidentDocument>>();
        builder.Services.AddSingleton<IIncidentHandlerManagementService, IncidentHandlerManagementService>();
        builder.Services.AddSingleton<IIncidentFilterManagementService, IncidentFilterManagementService>();
        builder.Services.AddSingleton<IInstructionGenerationService, InstructionGenerationService>();
        builder.Services.AddSingleton<IIncidentHandlingService, IncidentHandlingService>();

        // Register HttpClientService and configure HttpClient with proper BaseAddress
        builder.Services.AddSingleton<HttpClientService>();
        builder.Services.AddArmHelperHttpClient();
        builder.Services.AddRazorHttpClient();
        builder.Services.AddCrawlerHttpClient();

        builder.Services.AddSingleton<ILogAnalyticsService, LogAnalyticsService>();
        builder.Services.AddSingleton<ILogAnalysisService, LogAnalysisService>();

        // Configure chat services
        builder.Services.ConfigureIChatCompletionService()
                       .ConfigureAzureOpenAIClient()
                       .ConfigureIChatClient()
                       .ConfigureIEmbeddingGenerator();


        // Register all SubAgent types
        foreach (var agentType in SubAgentDiscovery.DiscoverSubAgentTypes())
        {
            builder.Services.AddSingleton(agentType);
        }

        // Kick off background processes
        if (!isFirstAgent)
        {
            builder.Services.AddHostedService<TimerService>();
        }

        // Kick off MCP Server Initializer
        builder.Services.AddSingleton<MCPMetaAgent>();
        builder.Services.AddHostedService<MCPMetaAgentManagementService>();

        builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();
        builder.Services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();
        builder.Services.AddSingleton<IBot, TeamsBot>()
                        .AddSingleton<IBotPollingMessage, TeamsBot>();
        // Add the new polling service
        builder.Services.AddHostedService<TeamsMessagePollingService>();

        builder.Services.AddDurableTaskWorker(b =>
        {
            b.AddTasks(r =>
            {
                DurableHelper.AddAllGeneratedTasks(r);
                if (isFirstAgent)
                {
                    FirstPartyDurableHelper.AddAllGeneratedTasks(r);
                }
            });

            string durableConnectionString = builder.ResolveDtsConnectionString();
            b.UseDurableTaskScheduler(durableConnectionString);

            builder.Services.AddOptions<DurableTaskSchedulerWorkerOptions>(b.Name).Configure<IServiceProvider>((option, sp) =>
            {
                var authService = sp.GetRequiredService<IAuthenticationService>();
                var tokenCredential = authService.GetDtsCredential();

                option.Credential = tokenCredential;
            });
        });

        builder.Services.AddDurableTaskClient(b =>
        {
            string durableConnectionString = builder.ResolveDtsConnectionString();
            b.UseDurableTaskScheduler(durableConnectionString);

            builder.Services.AddOptions<DurableTaskSchedulerClientOptions>(b.Name).Configure<IServiceProvider>((option, sp) =>
            {
                var authService = sp.GetRequiredService<IAuthenticationService>();
                var tokenCredential = authService.GetDtsCredential();

                option.Credential = tokenCredential;
            });
        });

        builder.Services.AddCosmosClient();

        ConfigureLogger(builder);

        // Register TeamsConnector service
        builder.Services.AddSingleton<TeamsConnector>();

        // Add services to the container.
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddControllersWithViews()
            .AddJsonOptions(options =>
            {
                // Allow HTML in JSON responses
                options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
                // Convert enum values as strings
                options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            });

        // Add Blazor services
        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();

        // Add GraphService registration
        builder.Services.AddSingleton<IGraphService, GraphService>();        // Add Websocket service registration

        builder.Services.AddOpenTelemetry().WithTracing(tracingBuilder =>
        {
            tracingBuilder.AddSource("SREAgent")
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
                // In-memory exporter for development - direct implementation
                exporters.Add(new InMemoryActivityExporter(exportedActivities));
                // }

                // Add Azure Data Explorer exporter for production if configured
                if (builder.Environment.IsProduction() &&
                    azureSettings?.AgentTraceKusto != null &&
                    !string.IsNullOrEmpty(azureSettings.AgentTraceKusto.ClusterUri))
                {
                    try
                    {
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
        });
        builder.Services.AddSingleton(TracerProvider.Default.GetTracer("SREAgent"));

        return builder;
    }

    private static TracerProvider GetTracerProvider(ResourceBuilder resourceBuilder, AzureSettings azureSettings, LoggingSettings? loggingSettings)
    {
        TracerProviderBuilder builder = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddSource("Microsoft.SemanticKernel*");

        if (loggingSettings?.LogGenAICalls == true)
        {
            builder = builder.AddConsoleExporter();
        }

        if (!string.IsNullOrEmpty(azureSettings.AppInsights.ConnectionString))
        {
            builder = builder.AddAzureMonitorTraceExporter(options => options.ConnectionString = azureSettings.AppInsights.ConnectionString);
        }


        return builder.Build();
    }

    private static MeterProvider GetMeterProvider(ResourceBuilder resourceBuilder, AzureSettings azureSettings)
    {
        MeterProviderBuilder builder = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddMeter("Microsoft.SemanticKernel*");

        if (!string.IsNullOrEmpty(azureSettings.AppInsights.ConnectionString))
        {
            builder = builder.AddAzureMonitorMetricExporter(options => options.ConnectionString = azureSettings.AppInsights.ConnectionString);
        }

        return builder.Build();
    }

    private static void ConfigureLogger(WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();

        if (builder.Environment.IsDevelopment())
        {
            builder.Logging.AddConsole();
        }

        ConfigureKustoLoggers(builder);
        ConfigureApplicationInsightsLoggers(builder);
        ConfigureAgentActionLoggers(builder);
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

        builder.Services.AddSingleton<CustomerLogger>(customerLogger);
        builder.Services.AddSingleton<CustomerAuditLogger>(customerAuditLogger);

        builder.Services.AddOpenTelemetry().WithTracing(tracingBuilder =>
        {
            tracingBuilder
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("AgentService"))
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddProcessor(new CustomerAuditTraceFilteringProcessor(customerAuditLogger));

            if (builder.Environment.IsDevelopment())
            {
                // local
                //tracingBuilder.AddConsoleExporter();
            }
        });
    }


    private static void ConfigureAgentActionLoggers(WebApplicationBuilder builder)
    {
        var ClusterUri = GetInternalKustoClusterConfiguration("ClusterUri");
        var DatabaseName = GetInternalKustoClusterConfiguration("DatabaseName");
        var CertificatePath = GetInternalKustoClusterConfiguration("CertificatePath");
        var FirstPartyAppClientId = GetInternalKustoClusterConfiguration("FirstPartyAppClientId");
        var FirstPartyAppTenantId = GetInternalKustoClusterConfiguration("FirstPartyAppTenantId");

        builder.Services.AddSingleton<AgentActionLogger>();
        if (builder.Environment.IsProduction())
        {
            // Add IAgentActionLogExporter as a singleton service
            builder.Services.AddSingleton<IAgentActionLogExporter>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<AgentActionLogADXExporter>>();
                return new AgentActionLogADXExporter(
                    ClusterUri,
                    DatabaseName,
                    "AgentActionLog",
                    CertificatePath,
                    FirstPartyAppClientId,
                    FirstPartyAppTenantId,
                    logger);
                // Enable batch processing for better performance
            });
        }
        else
        {
            // Fallback to console exporter if no ADX configuration is provided
            builder.Services.AddSingleton<IAgentActionLogExporter>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<AgentActionLogConsoleExporter>>();
                return new AgentActionLogConsoleExporter(logger, false); // Enable batch processing for better performance
            });
        }
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

    private static string GetApplicationInsightsConnectionString(WebApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment())
        {
            // Get Azure Settings for App Insights configuration
            var azureSettings = builder.Configuration.GetSection("AppSettings:Core:Azure").Get<AzureSettings>();
            var loggingSettings = builder.Configuration.GetSection("Logging").Get<LoggingSettings>();
            return azureSettings?.AppInsights?.ConnectionString;
        }

        return Environment.GetEnvironmentVariable("AppSettings__Core__Azure__ApplicationInsights__ConnectionString") ?? string.Empty;
    }

    // Helper method to get Azure Portal domains
    private static string[] GetAzurePortalDomains(IConfiguration configuration)
    {
        string azurePortalDomains = "";
        var configDomains = configuration.GetValue<string>("AppSettings:AzurePortalDomains");
        if (!string.IsNullOrEmpty(configDomains))
        {
            azurePortalDomains = configDomains;
        }

        return azurePortalDomains.Split(',');
    }
}
