using Agent.Core.Models;
using Agent.Core.Helpers;
using Agent.Runtime.SubAgents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Agent.Core.Configuration;
using Agent.Plugins;
using Microsoft.SemanticKernel;
using Agent.Data.DatabaseManagers.GraphDatabase;
using AIMessage = Microsoft.Extensions.AI.ChatMessage;
using Agent.Plugins.Definitions;
using Agent.Plugins.PeriodicMonitor;
using Agent.Plugins.Implementation;

namespace Agent.Runtime.Services
{
    public class AgentManager : IAgentManager, IDisposable
    {
        private const string ROOT_AGENT_PATH = "/";
        private readonly Dictionary<string, Session> _sessions = new();
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AgentManager> _logger;
        private readonly Dictionary<string, Type> _subAgentPathMapping;
        private readonly Agent _rootAgent;
        private readonly ServiceProvider _agentServiceProvider;

        public AgentManager(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _logger = _serviceProvider.GetRequiredService<ILogger<AgentManager>>();

            // Create a new ServiceCollection and configure it
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddProvider(_serviceProvider.GetRequiredService<ILoggerProvider>()));
            services.AddSingleton(_serviceProvider.GetRequiredService<IConfiguration>());
            services.AddSingleton(_serviceProvider.GetRequiredService<IOptions<AzureSettings>>());

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
                .AddSingleton<RemediationPluginDefinition>() 
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
                        @"You are SRE Agent. You must delegate to specific agents based on the question:
                        - For architecture-related questions, use launch_architecture_agent
                        - For logs and metrics related questions, use analyze_logs_and_metrics
                        - For questions that cannot be answered by all other agents, use launch_generic_agent
                        Always delegate to the appropriate agent rather than trying to answer directly.",
                        s.GetRequiredService<Kernel>(),
                        s.GetRequiredService<IOptions<AzureSettings>>(),
                        s.GetRequiredService<Microsoft.Extensions.AI.IChatClient>(),
                        s.GetRequiredService<ILoggerFactory>().CreateLogger<Agent>());

                    agent.Kernel.Plugins.AddFromObject(s.GetRequiredService<MetaAgentPlugin>(), "MetaAgentPlugin");
                    return agent;
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

        public Task<string> StartChatThread(string path, string threadId)
        {
            try
            {
                Session session;
                IAgent agent;
                Type expectedAgentType = path == ROOT_AGENT_PATH ? typeof(Agent) : _subAgentPathMapping[path];

                // Check if thread exists
                if (_sessions.TryGetValue(threadId, out var existingSession))
                {
                    _logger.LogInformation($"Found existing thread '{threadId}'");
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
                            return Task.FromResult(threadId);
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
                        return Task.FromResult(threadId);
                    }

                    // Create new agent for existing session
                    _logger.LogInformation($"Creating new agent for existing session with type: {expectedAgentType.Name}");
                    agent = CreateAgentByType(path);
                    session.AddAgent(agent);
                    session.SetCurrentAgent(agent.Name);
                    return Task.FromResult(threadId);
                }

                // If thread doesn't exist, create a new one
                _logger.LogInformation($"Starting new chat thread for path: '{path}'");
                session = new Session(_logger);
                session.ConfigureSession(id: threadId);
                _sessions[threadId] = session;

                // Create the appropriate agent based on path
                agent = CreateAgentByType(path);
                _logger.LogInformation($"Configuring agent: {agent.Name} for type: {agent.GetType().Name}");
                session.AddAgent(agent);
                session.SetCurrentAgent(agent.Name);

                return Task.FromResult(threadId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error starting chat thread for path '{path}'");
                throw;
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

        public async Task<Core.Models.ChatMessage> TrackChatThread(string threadId, string message)
        {
            try
            {
                if (!_sessions.TryGetValue(threadId, out var session))
                {
                    _logger.LogError($"Thread '{threadId}' not found");
                    throw new KeyNotFoundException($"Thread '{threadId}' not found");
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
            var threads = _sessions
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
                        _logger.LogWarning(ex, "Error getting agent type for session {SessionId}, defaulting to Meta", s.Key);
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

            _logger.LogInformation("Retrieved {Count} chat threads", threads.Count);
            foreach (var thread in threads)
            {
                _logger.LogDebug("Thread: {Id} - {Name} - {AgentType}", thread.Id, thread.Name, thread.AgentType);
            }

            return threads;
        }

        public void Dispose()
        {
            _sessions.Clear();
            (_rootAgent as IDisposable)?.Dispose();
            _agentServiceProvider?.Dispose();
        }
    }
}
