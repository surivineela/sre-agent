// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace FirstPartyAgent.Core.Models
{
    public class AgentPromptModel
    {
        public string AgentName { get; set; }
        public string Description { get; set; }
        public string SystemMessage { get; set; }

        public AgentPromptModel(string agentName, string description, string systemMessage)
        {
            AgentName = agentName;
            Description = description;
            SystemMessage = systemMessage;
        }
    }
}

