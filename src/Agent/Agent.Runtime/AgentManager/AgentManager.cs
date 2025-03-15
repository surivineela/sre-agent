using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Agent.Core.Models;

using CoreModels = Agent.Core.Models;

namespace Agent.Runtime.Services
{
    public class AgentManager : IAgentManager, IDisposable
    {
        private const string ROOT_AGENT_PATH = "/";
        private readonly ConcurrentDictionary<string, Session> _sessions = new();
        private readonly ILogger<AgentManager> _logger;
        private readonly Dictionary<string, Type> _subAgentPathMapping;
        private readonly Agent _rootAgent;
        private readonly IEnumerable<SubAgent> _subAgents;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private ConcurrentDictionary<Type, SubAgent> _subAgentRegistry;
        private Dictionary<string, IAgent> _agentInstances;

        public AgentManager(
            ILogger<AgentManager> logger,
            Kernel kernel,
            ILoggerFactory loggerFactory,
            IChatClient chatClient,
            MetaAgentPlugin metaAgentPlugin,
            OpenAISettings openAISettings,
            IHttpContextAccessor httpContextAccessor,
            IEnumerable<SubAgent> subAgents)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _subAgents = subAgents;
            _subAgentPathMapping = DiscoverSubAgentPaths();
            _subAgentRegistry = new ConcurrentDictionary<Type, SubAgent>();
            _agentInstances = new Dictionary<string, IAgent>(StringComparer.OrdinalIgnoreCase);

            // Get the pre-configured root agent
            _rootAgent = new Agent(
                        "main",
                        @"You are SRE Agent with a bunch of sub-agents that have specific skills and tools.
                        You are responsible for planning and use these sub-agents to get detailed information and make final decision.
                        For these specific sub-agents, you need to invoke registered functions to use them, these functions have one input for question and the output is the answer from subagent to this question. For example:
                        - logs_and_metrics_agent: it is the sub-agent which contains skill to fetch and analysis logs and metrics.
                        - diagnose_agent: it is the sub-agent which contains skill to diagnose app service apps.
                        - mcp_meta_agent: This agent can delegate to subagents which call tools on customer tool servers. If you are ever asked to do something and you don't know how, check to see if there is a tool that can do it for you. Check here
                            before you attempt to call generic_agent
                        - generic_agent: It is the most powerful sub-agent for general questions including get approval, get current time, scale/restart appservice, collect memory dump for app service, etc.
                            Always try to ask questions to generic_agent if other agents can't give you the good answer, but only after first checking with mcp_meta_agent
                        You can even ask these sub-agents about what they can do.
                        Try to ask questions to appropriate sub-agent to gather as more information as possible if you don't have access, permissions or just feel answer is not perfect to user's questions.",
                        kernel,
                        openAISettings,
                        chatClient,
                        loggerFactory.CreateLogger<Agent>());

            _rootAgent.Kernel.Plugins.AddFromObject(metaAgentPlugin, "MetaAgentPlugin");
            _logger.LogInformation("Retrieved singleton root Agent instance");

            // Add root agent to instances
            _agentInstances[ROOT_AGENT_PATH] = _rootAgent;

            // Initialize all subagents
            InitializeSubAgents();
        }

        private void InitializeSubAgents()
        {

            // Register all available subagents by their type (moved from SubAgentRegistry)
            foreach (var agent in _subAgents)
            {
                var type = agent.GetType();
                if (_subAgentRegistry.TryAdd(type, agent))
                {
                    _logger.LogInformation($"Registered SubAgent of type {type.Name}");
                }
                else
                {
                    _logger.LogWarning($"Failed to register SubAgent of type {type.Name} - duplicate registration");
                }
            }

            _logger.LogInformation($"Agent registry initialized with {_subAgentRegistry.Count} agents");

            _logger.LogInformation("Initializing all subagent instances from registry...");

            foreach (var pathTypePair in _subAgentPathMapping)
            {
                try
                {
                    var path = pathTypePair.Key;
                    var subAgentType = pathTypePair.Value;

                    var agent = GetSubAgent(subAgentType);
                    if (agent == null)
                    {
                        _logger.LogError($"Failed to get SubAgent instance for type {subAgentType.FullName} from registry");
                        continue;
                    }

                    _agentInstances[path] = agent;
                    _logger.LogInformation($"Added SubAgent instance of type {subAgentType.Name} for path {path}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error getting agent from registry for path: {pathTypePair.Key}");
                }
            }

            _logger.LogInformation($"Successfully initialized {_agentInstances.Count - 1} subagent instances from registry");
        }

        // Moved from SubAgentRegistry
        private SubAgent GetSubAgent(Type subAgentType)
        {
            if (_subAgentRegistry.TryGetValue(subAgentType, out var agent))
            {
                return agent;
            }

            _logger.LogWarning($"No SubAgent found for type {subAgentType.FullName}");
            return null;
        }

        // Moved from SubAgentRegistry
        public IEnumerable<SubAgent> GetAllAgents() => _subAgentRegistry.Values;

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
            if (_httpContextAccessor?.HttpContext?.Items != null &&
                _httpContextAccessor.HttpContext.Items.TryGetValue("LastRespondingAgent", out var finalAgent))
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
                if (_agentInstances.TryGetValue(path, out var agent))
                {
                    _logger.LogInformation($"Using cached agent instance for path: {path}");
                    return agent;
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

        public async Task<CoreModels.ChatMessage> TrackChatThread(string chatId, string message)
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

                return response.FirstOrDefault() ?? new CoreModels.ChatMessage
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

            // Dispose all agent instances
            foreach (var agent in _agentInstances.Values)
            {
                (agent as IDisposable)?.Dispose();
            }
            _agentInstances.Clear();
        }
    }
}
