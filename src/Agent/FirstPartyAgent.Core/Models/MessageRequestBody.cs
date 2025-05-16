// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace FirstPartyAgent.Core.Models
{
    public class MessageRequestBody
    {
        public string Sender { get; set; }
        public string Message { get; set; }
        public string AgentMode { get; set; }
        public string SessionId { get; set; }
        public string? Title { get; set; }
        public Dictionary<string, string> PromptReplacements { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
    }
}

