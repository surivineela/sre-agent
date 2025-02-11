using Agent.Core.Models;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Agent.Core
{
    public class Session
    {
        private Agent? _currentAgent;
        private readonly Dictionary<string, Agent> _agents = [];
        
        // Chat history is used to store the conversation context for the LLM to process
        private readonly ChatHistory _chatHistory = [];

        // Messages store the conversation history publicly visible to the user
        private readonly List<ChatMessage> _messages = [];

        public void AddAgent(Agent agent)
        {
            _agents.Add(agent.Name, agent);
            
            if (_currentAgent == null)
            {
                _currentAgent = agent;
            }
        }

        public void SetCurrentAgent(string agentName)
        {
            if (!_agents.TryGetValue(agentName, out _currentAgent))
            {
                throw new ArgumentException($"Agent with name {agentName} does not exist");
            }
        }

        public Agent GetCurrentAgent()
        {
            if (_currentAgent == null)
            {
                throw new ArgumentNullException("Current agent hasn't been set");
            }
            return _currentAgent;
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
            if (_chatHistory.Count == 0)
            {
                return;
            }

            var lastChatMessage = _chatHistory.Last();
            if (lastChatMessage.Role != AuthorRole.User)
            {
                return;
            }

            var currentAgent = GetCurrentAgent();
            var result = await currentAgent.RunFullTurnAsync(_chatHistory);

            _messages.Add(new ChatMessage()
            {
                Message = result.ToString(),
                IsUser = false,
                Timestamp = DateTime.Now
            });
        }
    }
}
