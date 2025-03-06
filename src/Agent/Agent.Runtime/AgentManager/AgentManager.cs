using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Models;
using Agent.Data.DatabaseManagers.GraphDatabase;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Plugins.Implementation;
using Agent.Plugins.PeriodicMonitor;
using Agent.Runtime.SubAgents;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Agent.Plugins.CodeAnalyzer;
using Agent.Plugins.Models;
using Agent.Graph.Crawler.ARM;
using Azure.Identity;
using Azure.ResourceManager;

namespace Agent.Runtime.Services
{
    public class AgentManager : IAgentManager, IDisposable
    {
        private const string ROOT_AGENT_PATH = "/";
        private readonly ConcurrentDictionary<string, Session> _sessions = new();
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AgentManager> _logger;
        private readonly Dictionary<string, Type> _subAgentPathMapping;
        private readonly Agent _rootAgent;
        private readonly ServiceProvider _agentServiceProvider;

        public AgentManager(IServiceProvider serviceProvider, AppSettings appSettings, AzureSettings azureSettings, ExternalSettings externalSettings)
        {
            _serviceProvider = serviceProvider;
            _logger = _serviceProvider.GetRequiredService<ILogger<AgentManager>>();

            // Create a new ServiceCollection and configure it
            IServiceCollection services = new ServiceCollection();
            services.AddLogging(builder => builder.AddProvider(_serviceProvider.GetRequiredService<ILoggerProvider>()));

            // Pass through configurations from parent service collection
            services.AddSingleton(appSettings);
            services.AddSingleton(azureSettings);
            services.AddSingleton(externalSettings);
            services.RegisterInnerAppSettings<AppSettings>();

            // Configure agent-specific services
            ConfigureAgentServices(services);

            // Build the service provider for agents
            _agentServiceProvider = services.BuildServiceProvider();

            _subAgentPathMapping = DiscoverSubAgentPaths();
            // Get the pre-configured root agent from the new service provider
            _rootAgent = _agentServiceProvider.GetRequiredService<Agent>();
            _logger.LogInformation("Retrieved singleton root Agent instance from service provider");
        }

        private void ConfigureAgentServices(IServiceCollection services)
        {
            _ = services
                .ConfigureIChatCompletionService()
                .ConfigureAzureOpenAIClient()
                .ConfigureIChatClient()
                .AddHttpContextAccessor()
                .AddTransient<Kernel>(sp => new Kernel(sp))
                .AddSingleton<MetaAgentPlugin>()
                .AddSingleton<ISubscriptionPlugin, SubscriptionPlugin>()
                .AddSingleton<AzureResourceGraphClient>()
                .AddSingleton<ArmResourceCrawlerFactory>()
                .AddSingleton<ResourceGraphCrawler>()
                .AddSingleton<SubscriptionPluginDefinition>()
                .AddSingleton<IGraphDatabaseManager, GremlinGraphDatabaseManager>()
                .AddSingleton<IGraphDBPlugin, GraphDBPlugin>()
                .AddSingleton<GraphDBPluginDefinition>()
                .AddSingleton<ITimePlugin, TimePlugin>()
                .AddSingleton<TimePluginDefinition>()
                .AddSingleton<IMetricsPlugin, MetricsPlugin>()
                .AddSingleton<MetricsPluginDefinition>()
                .AddSingleton<IDiagnosePlugin, DiagnosePlugin>()
                .AddSingleton<DiagnosePluginDefinition>()
                .AddSingleton<IMonitorPlugin, MonitorPlugin>()
                .AddSingleton<MonitorPluginDefinition>()
                .AddSingleton<IPeriodicMonitor, PeriodicMonitor>()
                .AddSingleton<ICurrentStatePlugin, CurrentStatePlugin>()
                .AddSingleton<CurrentStatePluginDefinition>()
                .AddSingleton<IRemediationPlugin, RemediationPlugin>()
                .AddSingleton<TeamsConnector>()
                .AddSingleton<GitHubClient>()
                .AddSingleton<CodeAnalyzerService>()
                .AddSingleton<ICodeAnalyzerPlugin, CodeAnalyzerPlugin>()
                .AddSingleton<RemediationPluginDefinition>()
                .AddSingleton<ApprovalPlugin>()
                // Register all SubAgent types as singletons
                .AddSingleton<MetaAgentPlugin>()
                .AddSingleton<GraphDBQueryAgent>()
                .AddSingleton<ArchitectureAgent>()
                .AddSingleton<GenericAgent>()
                .AddSingleton<LogsAndMetricsAgent>()
                .AddSingleton<DiagnosticAgent>()
                // Add logger factory from parent service provider
                .AddSingleton(_serviceProvider.GetRequiredService<ILoggerFactory>())
                // Register the root Agent with explicit logger
                .AddSingleton(s =>
                {
                    var agent = new Agent(
                        "main",
                        @"You are SRE Agent with a bunch of sub-agents that have specific skills and tools.
                        You are responsible for planning and use these sub-agents to get detailed information and make final decision.
                        For these specific sub-agents, you need to invoke registered functions to use them, these functions have one input for question and the output is the answer from subagent to this question. For example:
                        - logs_and_metrics_agent: it is the sub-agent which contains skill to fetch and analysis logs and metrics.
                        - diagnose_agent: it is the sub-agent which contains skill to diagnose app service apps.
                        - generic_agent: it is the most powerful sub-agent for general questions including get approval, get current time, scale/restart appservice, collect memory dump for app service, etc. Always try to ask questions to generic_agent if other agents can't give you the good answer.
                        You can even ask these sub-agents about what they can do.
                        Try to ask questions to appropriate sub-agent to gather as more information as possible if you don't have access, permissions or just feel answer is not perfect to user's questions.",
                        s.GetRequiredService<Kernel>(),
                        s.GetRequiredService<OpenAISettings>(),
                        s.GetRequiredService<Microsoft.Extensions.AI.IChatClient>(),
                        s.GetRequiredService<ILoggerFactory>().CreateLogger<Agent>());

                    agent.Kernel.Plugins.AddFromObject(s.GetRequiredService<MetaAgentPlugin>(), "MetaAgentPlugin");
                    return agent;
                });

            // register arm client for crawler
            services.AddKeyedSingleton("CrawlerArmClient", (sp, _) =>
            {
                var crawlerSettings = sp.GetRequiredService<CrawlerSettings>();
                var credOptions = new DefaultAzureCredentialOptions();
                if (!string.IsNullOrEmpty(crawlerSettings.IdentityClientId))
                {
                    credOptions.ManagedIdentityClientId = crawlerSettings.IdentityClientId;
                }
                return new ArmClient(new DefaultAzureCredential(credOptions));
            });
        }

        private record SubAgentInfo(Type AgentType, string Name);

        private Dictionary<string, Type> DiscoverSubAgentPaths()
        {
            var mapping = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            _logger.LogInformation("Starting subagent path discovery...");

            var subAgentTypes = SubAgentDiscovery.DiscoverSubAgentTypes();
            _logger.LogInformation($"Found {subAgentTypes.Count()} subagent types");

            foreach (var agentType in subAgentTypes)
            {
                var path = SubAgentDiscovery.GeneratePathFromAgentType(agentType);
                if (string.IsNullOrEmpty(path))
                {
                    _logger.LogWarning($"Generated path is null or empty for agent type: {agentType.FullName}");
                    continue;
                }

                mapping[path] = agentType;
                _logger.LogInformation($"Registered subagent path: '{path}' -> {agentType.FullName}");
            }

            return mapping;
        }

        public IEnumerable<string> GetAvailableSubAgents()
        {
            return new[] { ROOT_AGENT_PATH }.Concat(_subAgentPathMapping.Keys);
        }

        public Task<string> StartChatThread(string path, string chatId)
        {
            try
            {
                Session session;
                IAgent agent;
                Type expectedAgentType = path == ROOT_AGENT_PATH ? typeof(Agent) : _subAgentPathMapping[path];

                // Check if thread exists
                if (_sessions.TryGetValue(chatId, out var existingSession))
                {
                    _logger.LogInformation($"Found existing thread '{chatId}'");
                    session = existingSession;

                    // Store the current path in the session
                    session.CurrentPath = path;

                    // Check if the agent type matches the requested path
                    try
                    {
                        agent = session.GetCurrentAgent();
                        if (agent.GetType() == expectedAgentType)
                        {
                            _logger.LogInformation($"Reusing existing agent of type {expectedAgentType.Name}");
                            return Task.FromResult(chatId);
                        }
                        _logger.LogInformation($"Agent type mismatch. Expected: {expectedAgentType.Name}, Found: {agent.GetType().Name}");
                    }
                    catch (ArgumentNullException)
                    {
                        _logger.LogInformation("No current agent set in session");
                    }

                    // Check if the session has this agent type already
                    if (session.HasAgent(expectedAgentType.Name))
                    {
                        session.SetCurrentAgent(session.GetAgentByType(expectedAgentType.Name).Name);
                        _logger.LogInformation($"Reusing existing agent of type {expectedAgentType.Name}");
                        return Task.FromResult(chatId);
                    }

                    // Create new agent for existing session
                    _logger.LogInformation($"Creating new agent for existing session with type: {expectedAgentType.Name}");
                    agent = CreateAgentByType(path);
                    session.AddAgent(agent);
                    session.SetCurrentAgent(agent.Name);
                    return Task.FromResult(chatId);
                }

                // If thread doesn't exist, create a new one
                _logger.LogInformation($"Starting new chat thread for path: '{path}'");
                session = new Session(_logger);
                session.ConfigureSession(id: chatId);
                _sessions.TryAdd(chatId, session);

                // Create the appropriate agent based on path
                agent = CreateAgentByType(path);
                _logger.LogInformation($"Configuring agent: {agent.Name} for type: {agent.GetType().Name}");
                session.AddAgent(agent);
                session.SetCurrentAgent(agent.Name);

                return Task.FromResult(chatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error starting chat thread for path '{path}'");
                throw;
            }
        }

        public async IAsyncEnumerable<string> StreamChatThread(
    string chatId, string message,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!_sessions.TryGetValue(chatId, out var session))
            {
                _logger.LogError($"Chat '{chatId}' not found");
                yield return $"Error: Chat '{chatId}' not found";
                yield break;
            }

            var messageTime = session.AddUserMessage(message);

            // Make sure we're using the correct agent type before processing
            var currentAgent = session.GetCurrentAgent();
            _logger.LogInformation($"Processing streaming message with agent: {currentAgent.Name} of type {currentAgent.GetType().Name}");

            // Stream from the agent through the session's ProcessStreamAsync method
            await foreach (var chunk in session.ProcessStreamAsync(cancellationToken))
            {
                yield return chunk;
            }

            // Get the LastRespondingAgentType from the session and set it in HttpContext
            var httpContextAccessor = _agentServiceProvider.GetService<IHttpContextAccessor>();
            if (httpContextAccessor?.HttpContext?.Items != null &&
        httpContextAccessor.HttpContext.Items.TryGetValue("LastRespondingAgent", out var finalAgent))
            {
                _logger.LogInformation($"Final LastRespondingAgent set in HttpContext: {finalAgent}");
            }
            else
            {
                _logger.LogWarning("No LastRespondingAgent set in HttpContext after processing");
            }
        }

        private IAgent CreateAgentByType(string path)
        {
            try
            {
                if (path == ROOT_AGENT_PATH)
                {
                    _logger.LogInformation("Using singleton root Agent instance");
                    return _rootAgent;
                }

                if (_subAgentPathMapping.TryGetValue(path, out var subAgentType))
                {
                    try
                    {
                        var agent = ActivatorUtilities.CreateInstance(_agentServiceProvider, subAgentType) as SubAgent;
                        if (agent == null)
                        {
                            throw new InvalidOperationException($"Failed to create SubAgent instance for type {subAgentType.FullName}");
                        }
                        _logger.LogInformation($"Created SubAgent instance of type {subAgentType.Name}");
                        return agent;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error creating agent for path: {path}. Falling back to root agent.");
                        return _rootAgent;
                    }
                }

                _logger.LogWarning($"No agent registered for path: {path}. Using root agent.");
                return _rootAgent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in CreateAgentByType for path: {path}. Using root agent.");
                return _rootAgent;
            }
        }

        public async Task<Core.Models.ChatMessage> TrackChatThread(string chatId, string message)
        {
            try
            {
                if (!_sessions.TryGetValue(chatId, out var session))
                {
                    _logger.LogError($"Thread '{chatId}' not found");
                    throw new KeyNotFoundException($"Thread '{chatId}' not found");
                }

                var messageTime = session.AddUserMessage(message);

                // Make sure we're using the correct agent type before processing
                var currentAgent = session.GetCurrentAgent();
                _logger.LogInformation($"Processing message with agent: {currentAgent.Name} of type {currentAgent.GetType().Name}");

                await session.ProcessAsync(CancellationToken.None);

                var response = session.GetMessages(messageTime);
                if (!response.Any())
                {
                    _logger.LogWarning("No response received from agent");
                }

                return response.FirstOrDefault() ?? new Core.Models.ChatMessage
                {
                    Message = "No response from agent",
                    IsUser = false,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking chat thread");
                throw;
            }
        }

        public List<ChatThreadInfo> GetChatThreads()
        {
            var chats = _sessions
                .Select(s =>
                {
                    string agentType;
                    try
                    {
                        var currentAgent = s.Value.GetCurrentAgent();
                        agentType = currentAgent?.GetType().Name.Replace("Agent", "") ?? "Meta";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error getting agent type for session {ChatId}, defaulting to Meta", s.Key);
                        agentType = "Meta";
                    }

                    return new ChatThreadInfo
                    {
                        Id = s.Key,
                        Name = s.Value.Name,
                        AgentType = agentType,
                        CreatedAt = s.Value.CreatedAt
                    };
                })
                .ToList();

            _logger.LogInformation("Retrieved {Count} chat threads", chats.Count);
            foreach (var chat in chats)
            {
                _logger.LogDebug("Chat: {Id} - {Name} - {AgentType}", chat.Id, chat.Name, chat.AgentType);
            }

            return chats;
        }

        public void Dispose()
        {
            _sessions.Clear();
            (_rootAgent as IDisposable)?.Dispose();
            _agentServiceProvider?.Dispose();
        }
    }
}
