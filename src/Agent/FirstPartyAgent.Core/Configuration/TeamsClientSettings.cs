// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace FirstPartyAgent.Core.Configuration
{
    public class TeamsClientSettings
    {
        public string TeamsEndpoint {  get; set; }
        public string TeamsGroupConversationId { get; set; }
        public bool SendLogsToTeams { get; set; }
        public Dictionary<string, string> AgentConversationIds { get; set; } = new Dictionary<string, string>();
    }
}

