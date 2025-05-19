using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FirstPartyAgent.Core.Helpers;
using FirstPartyAgent.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace FirstPartyAgent.Core.Models
{
     public class SessionInformation
    {
        public string SessionId { get; set; }
        public AgentMode AgentMode { get; set; }
        public DateTime Timestamp { get; set; }
        public ChatHistory ChatHistory { get; set; }
        public bool AgentLoopRunning { get; set; }
        public Kernel Kernel { get; set; }
        public Dictionary<string, object> Data { get; set; }

        public SessionInformation(string sessionId, string agentMode)
        {
            AgentMode _agentMode = Enum.Parse<AgentMode>(agentMode);
            SessionId = sessionId;
            AgentMode = _agentMode;
            Timestamp = DateTime.UtcNow;
            ChatHistory = new ChatHistory();

            var agentInfo = AgentFinder.GetAgentPrompts(_agentMode).FirstOrDefault();
            ChatHistory.AddSystemMessage(agentInfo?.SystemMessage ?? "You are a helpful AI Assistant.");
        }
    }
}
