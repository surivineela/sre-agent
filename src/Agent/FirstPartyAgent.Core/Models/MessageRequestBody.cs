// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace FirstPartyAgent.Core.Models
{
    public class MessageRequestBody
    {
        public string Sender { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string AgentMode { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public bool SendLogsToTeams { get; set; } = false;
        public string? Title { get; set; }
        public Dictionary<string, string> PromptReplacements { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
    }
}

