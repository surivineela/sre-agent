// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace FirstPartyAgent.Core.Models
{
    public class AlertRequestBody
    {
        public string Source { get; set; } = string.Empty;
        public string IncidentId { get; set; } = string.Empty;
        public string? CustomMessage { get; set; } 
        public string AgentMode { get; set; } = string.Empty;
        public string? AlertId { get; set; }
        public string? AdditionalPayload { get; set; }
        public ICMAlertConfig? CustomAlertConfig { get; set; }
        public string? SessionId { get; set; }
    }
}

