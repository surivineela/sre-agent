// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace FirstPartyAgent.Core.Models
{
    public class AlertRequestBody
    {
        public string Source { get; set; }
        public string IncidentId { get; set; }
        public string? CustomMessage { get; set; }
        public string AgentMode { get; set; }
        public string? AlertId { get; set; }
        public string? AdditionalPayload { get; set; }
    }
}

