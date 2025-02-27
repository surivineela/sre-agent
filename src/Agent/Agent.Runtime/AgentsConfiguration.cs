using System.ComponentModel;
using Agent.Core.Configuration;
using Agent.Runtime.SubAgents;
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

namespace Agent.Runtime
{
    public static class AgentsConfigurationExtensions
    {
        /*
       public static void ConfigureAgents(this IServiceCollection services)
        {
            _ = services
                .ConfigureIChatCompletionService()
                .ConfigureAzureOpenAIClient()
                .ConfigureIChatClient()
                .AddTransient<Kernel>(sp =>
                {
                    return new Kernel(sp);
                })

                // Add plugins
                .AddSingleton<MetaAgentPlugin>()
                .AddSingleton<ISubscriptionPlugin, SubscriptionPlugin>()
                .AddSingleton<SubscriptionPluginDefinition>()
                .AddSingleton<MonitorPluginDefinition>()
                .AddSingleton<IGraphDatabaseManager, GremlinGraphDatabaseManager>()
                .AddSingleton<IGraphDBPlugin, GraphDBPlugin>()
                .AddSingleton<IMonitorPlugin, MonitorPlugin>()
                .AddSingleton<GraphDBPluginDefinition>()
                .AddSingleton<ITimePlugin, TimePlugin>()
                .AddSingleton<TimePluginDefinition>()
                .AddSingleton<IMetricsPlugin, MetricsPlugin>()
                .AddSingleton<MetricsPluginDefinition>()
                .AddSingleton<IDiagnosePlugin, DiagnosePlugin>()
                .AddSingleton<DiagnosePluginDefinition>()  
                .AddSingleton<ICurrentStatePlugin, CurrentStatePlugin>()
                .AddSingleton<CurrentStatePluginDefinition>()

                // Add agents
                .AddSingleton<GenericAgent>()
                .AddSingleton<GraphDBQueryAgent>()
                .AddSingleton<ArchitectureAgent>()
                .AddSingleton<IPeriodicMonitor, PeriodicMonitor>()
                .AddSingleton<LogsAndMetricsAgent>()
                .AddSingleton<DiagnosticAgent>()
                .AddSingleton<IRemediationPlugin, RemediationPlugin>()
                .AddSingleton<RemediationPluginDefinition>()
                // Agent is defined by its name, instructions, and the plugins it uses
                // In future we load the agent and conversation from a data store. For now it is all in memory
                .AddSingleton(s =>
                {
                    var agent = new Agent(
                        "main",
                        //IssueFinderAgent.SystemMessage,
                        @"You are SRE Agent. You must delegate to other agents",
                        s.GetRequiredService<Kernel>(),
                        s.GetRequiredService<IOptions<AzureSettings>>(),
                        s.GetRequiredService<ILogger<Agent>>());  // Add logger parameter

                    //agent.Kernel.Plugins.AddFromObject(s.GetRequiredService<SubscriptionPluginDefinition>(), "SubscriptionPlugin");
                    agent.Kernel.Plugins.AddFromObject(s.GetRequiredService<MetaAgentPlugin>(), "MetaAgentPlugin");

                    return agent;
                })
                .AddSingleton<Session>(s =>
                {
                    var conversation = new Session();
                    conversation.AddAgent(s.GetRequiredService<Agent>());
                    return conversation;
                });
        }
*/
        public static IServiceCollection ConfigureIChatCompletionService(this IServiceCollection services)
        {
            return services
                .AddSingleton<IChatCompletionService>(sp =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var azureSettings = config.GetSection("Azure").Get<AzureSettings>();
                    if (azureSettings == null)
                    {
                        throw new NullReferenceException("Azure settings are required.");
                    }
                    return new AzureOpenAIChatCompletionService(
                        deploymentName: azureSettings.OpenAI.DeploymentName,
                        endpoint: azureSettings.OpenAI.Endpoint,
                        apiKey: azureSettings.OpenAI.ApiKey
                    );
                });
        }

        public static IServiceCollection ConfigureAzureOpenAIClient(this IServiceCollection services)
        {
            return services
                .AddSingleton<AzureOpenAIClient>(sp =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var azureSettings = config.GetSection("Azure").Get<AzureSettings>();
                    if (azureSettings == null)
                    {
                        throw new NullReferenceException("Azure settings are required.");
                    }

                    return new AzureOpenAIClient(
                        endpoint: new Uri(azureSettings.OpenAI.Endpoint),
                        credential: new System.ClientModel.ApiKeyCredential(azureSettings.OpenAI.ApiKey)
                    );
                });
        }

        public static IServiceCollection ConfigureIChatClient(this IServiceCollection services)
        {
            return services
                .AddSingleton<IChatClient>(sp =>
                {
                    var client = sp.GetRequiredService<AzureOpenAIClient>();
                    var config = sp.GetRequiredService<IConfiguration>();
                    var azureSettings = config.GetSection("Azure").Get<AzureSettings>();
                    if (azureSettings == null)
                    {
                        throw new NullReferenceException("Azure settings are required.");
                    }

                    return new ChatClientBuilder(client.AsChatClient(azureSettings.OpenAI.DeploymentName)).Build();
                });
        }
    }
    public class MetaAgentPlugin
    {
        ILogger<MetaAgentPlugin> _logger;
        IChatClient _chatClient;
        ArchitectureAgent _badArchitectureAgent;
        GenericAgent _genericAgent;
        LogsAndMetricsAgent _logsAndMetricsAgent;
        IHttpContextAccessor _httpContextAccessor;

        public MetaAgentPlugin(IChatClient chatClient, ILogger<MetaAgentPlugin> logger, ArchitectureAgent badArchitectureAgent, GenericAgent genericAgent, LogsAndMetricsAgent logsAndMetricsAgent, IHttpContextAccessor httpContextAccessor)
        {
            _chatClient = chatClient;
            _logger = logger;
            _badArchitectureAgent = badArchitectureAgent;
            _genericAgent = genericAgent;
            _logsAndMetricsAgent = logsAndMetricsAgent;
            _httpContextAccessor = httpContextAccessor;
        }

        [KernelFunction("launch_architecture_agent")]
        [Description("This agent will answer questions relating to the architecture of a service.")]
        public async Task<string> LaunchBadArchitectureAgentAsync(string question)
        {
            _logger.LogInformation("Invoking architecture agent");
            string answer = await _badArchitectureAgent.Ask(question);
            _logger.LogInformation($"Architecture agent responded with: {answer}");

            if (_httpContextAccessor?.HttpContext?.Items != null)
            {
                _httpContextAccessor.HttpContext.Items["LastRespondingAgent"] = "Architecture";
            }

            return answer;
        }

        [KernelFunction("launch_generic_agent")]
        [Description("If you can't find a better agent, try this agent")]
        public async Task<string> LaunchGenericAgentAsync(string question)
        {
            _logger.LogInformation("Invoking generic agent");
            string answer = await _genericAgent.Ask(question);
            _logger.LogInformation($"Generic agent responded with: {answer}");

            if (_httpContextAccessor?.HttpContext?.Items != null)
            {
                _httpContextAccessor.HttpContext.Items["LastRespondingAgent"] = "Generic";
            }

            return answer;
        }

        [KernelFunction("analyze_logs_and_metrics")]
        [Description("This agent will answer questions relating to logs and metrics of a service.")]
        public async Task<string> LaunchLogsAndMetricsAgentAsync(string question)
        {
            _logger.LogInformation("Invoking LogsAndMetrics agent");
            string answer = await _logsAndMetricsAgent.Ask(question);
            _logger.LogInformation($"LogsAndMetrics agent responded with: {answer}");

            if (_httpContextAccessor?.HttpContext?.Items != null)
            {
                _httpContextAccessor.HttpContext.Items["LastRespondingAgent"] = "LogsAndMetrics";
            }

            return answer;
        }
    }
}
