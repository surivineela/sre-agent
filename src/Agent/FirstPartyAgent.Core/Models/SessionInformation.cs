using FirstPartyAgent.Core.Helpers;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace FirstPartyAgent.Core.Models
{
    public class SessionInformation
    {
        public string SessionId { get; set; } = string.Empty;
        public AgentMode AgentMode { get; set; }
        public DateTime Timestamp { get; set; }
        public bool SendLogsToTeams { get; set; } = false;
        public ChatHistory ChatHistory { get; set; } = new ChatHistory();
        public bool AgentLoopRunning { get; set; }

        public Dictionary<string, object> Data { get; set; } = [];

        public SessionInformation()
        {
        }

        public SessionInformation(string sessionId, string agentMode)
        {
            AgentMode _agentMode = Enum.Parse<AgentMode>(agentMode);
            SessionId = sessionId;
            AgentMode = _agentMode;
            Timestamp = DateTime.UtcNow;

            var agentInfo = AgentFinder.GetAgentPrompts(_agentMode).FirstOrDefault();
            ChatHistory.AddSystemMessage(agentInfo?.SystemMessage ?? "You are a helpful AI Assistant.");
        }

        public SessionInformation(DeserializableSessionInformation deserializableSessionInformation)
        {
#pragma warning disable SKEXP0001, SKEXP0101
            SessionId = deserializableSessionInformation.SessionId;
            AgentMode = deserializableSessionInformation.AgentMode;
            Timestamp = deserializableSessionInformation.Timestamp;
            ChatHistory = new ChatHistory();
            foreach (var m in deserializableSessionInformation.ChatHistory)
            {
                if (m.Role == AuthorRole.Tool || m.Content == null)
                {
                    continue;
                }
                var message = new ChatMessageContent(m.Role, m.Content);
                message.AuthorName = m.AuthorName;
                message.Source = m.Source;
                ChatHistory.Add(message);
            }
            AgentLoopRunning = deserializableSessionInformation.AgentLoopRunning;
            Data = deserializableSessionInformation.Data;
#pragma warning restore SKEXP0001, SKEXP0101
        }
    }

    public class DeserializableSessionInformation
    {
        public string SessionId { get; set; } = string.Empty;
        public AgentMode AgentMode { get; set; }
        public DateTime Timestamp { get; set; }
        public List<DeserializableChatMessageContent> ChatHistory { get; set; } = new List<DeserializableChatMessageContent>();
        public bool AgentLoopRunning { get; set; }
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();

        public DeserializableSessionInformation() { }
        public DeserializableSessionInformation(SessionInformation sessionInfo)
        {
#pragma warning disable SKEXP0001, SKEXP0101
            SessionId = sessionInfo.SessionId;
            AgentMode = sessionInfo.AgentMode;
            Timestamp = sessionInfo.Timestamp;
            AgentLoopRunning = sessionInfo.AgentLoopRunning;
            Data = sessionInfo.Data;
            ChatHistory = new List<DeserializableChatMessageContent>();
            foreach (var m in sessionInfo.ChatHistory)
            {
                if (m.Role == AuthorRole.Tool)
                {
                    continue;
                }
                var message = new DeserializableChatMessageContent(m);
                ChatHistory.Add(message);
            }
#pragma warning restore SKEXP0001, SKEXP0101
        }
    }

}
