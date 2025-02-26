using Agent.Core.Models;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime
{
    public class Session
    {
        private readonly ILogger _logger;
        private IAgent? _currentAgent;
        private readonly Dictionary<string, IAgent> _agents;
        private readonly ChatHistory _chatHistory = new();
        private readonly List<ChatMessage> _messages;
        public string LastRespondingAgentType { get; set; } = "Meta";
        public string CurrentPath { get; set; } = "/";
        private string? _sessionName;
        private string? _sessionId;
        private readonly Lazy<string> _lazySessionId;

        // TODO: session Name should be a good summary of the first question/answer in the session. 
        public string Name => _sessionName ?? $"Session-{DateTime.Now:yyyyMMddHHmmss}";
        public DateTime CreatedAt { get; } = DateTime.UtcNow;

        public Session(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _messages = new List<ChatMessage>();
            _agents = new Dictionary<string, IAgent>();
            _lazySessionId = new Lazy<string>(() => Guid.NewGuid().ToString());
            _logger.LogInformation("Session initialized");
        }

        public void ConfigureSession(string? name = null, string? id = null)
        {
            _sessionName = string.IsNullOrEmpty(name) ? $"Session-{DateTime.Now:yyyyMMddHHmmss}" : name;
            _sessionId = id;
            _logger.LogInformation("Session configured with Name: {Name}, Id: {Id}", _sessionName, id ?? "default");
        }

        public string GetSessionId()
        {
            return _sessionId ?? _lazySessionId.Value;
        }

        public string GetSessionName()
        {
            return Name;
        }

        public void AddAgent(IAgent agent)
        {
            try
            {
                _logger.LogInformation("Adding agent: {AgentName}", agent.Name);
                _agents.Add(agent.Name, agent);

                if (_currentAgent == null)
                {
                    _logger.LogInformation("Setting as current agent: {AgentName}", agent.Name);
                    _currentAgent = agent;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding agent: {AgentName}", agent.Name);
                throw;
            }
        }

        public void SetCurrentAgent(string agentName)
        {
            if (!_agents.TryGetValue(agentName, out _currentAgent))
            {
                throw new ArgumentException($"Agent with name {agentName} does not exist");
            }
        }

        public IAgent GetCurrentAgent()
        {
            if (_currentAgent == null)
            {
                throw new InvalidOperationException("No agent is currently set for this session");
            }
            return _currentAgent;
        }

        public bool HasAgent(string agentType)
        {
            return _agents.Values.Any(agent => agent.GetType().Name == agentType);
        }

        public IAgent GetAgentByType(string agentType)
        {
            return _agents.Values.First(agent => agent.GetType().Name == agentType);
        }

        public DateTime AddUserMessage(string message)
        {
            _chatHistory.AddUserMessage(message);

            DateTime currentTime = DateTime.Now;
            _messages.Add(new ChatMessage()
            {
                Message = message,
                IsUser = true,
                Timestamp = currentTime
            });

            return currentTime;
        }

        public List<ChatMessage> GetMessages(DateTime? since = null)
        {
            if (since == null)
            {
                return _messages;
            }
            return _messages.Where(m => m.Timestamp > since).ToList();
        }

        public async Task ProcessAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting ProcessAsync to agent {AgentName} for session {SessionId}", GetCurrentAgent().Name, GetSessionId());

                if (_chatHistory.Count == 0)
                {
                    _logger.LogInformation("Chat history is empty, skipping processing");
                    return;
                }
                // print _chatHistory
                
                foreach (var chatMessage in _chatHistory)
                {
                    _logger.LogInformation("ChatMessage: {ChatMessage}", chatMessage.Content);
                }

                var lastChatMessage = _chatHistory.Last();
                if (lastChatMessage.Role != AuthorRole.User)
                {
                    _logger.LogInformation("Last message is not from user, skipping processing");
                    return;
                }

                var currentAgent = GetCurrentAgent();

                string result;
                if (currentAgent is Agent mainAgent)
                {
                    // Use RunFullTurnAsync for the main Agent
                    var agentResult = await mainAgent.RunFullTurnAsync(_chatHistory);
                    result = agentResult.ToString();
                }
                else
                {
                    // Use Ask for SubAgents
                    result = await currentAgent.Ask(lastChatMessage.Content);
                }

                _logger.LogInformation("Agent {AgentName} processing completed successfully for session {SessionId}", GetCurrentAgent().Name, GetSessionId());

                _messages.Add(new ChatMessage()
                {
                    Message = result,
                    IsUser = false,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ProcessAsync");
                throw;
            }
        }

        public string GetCurrentAgentType()
        {
            try
            {
                return _currentAgent?.GetType().Name.Replace("Agent", "") ?? "Meta";
            }
            catch (Exception)
            {
                return "Meta";
            }
        }
    }
}
