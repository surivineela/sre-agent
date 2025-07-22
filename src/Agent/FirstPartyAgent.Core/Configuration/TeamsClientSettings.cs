// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace FirstPartyAgent.Core.Configuration
{
    public class TeamsClientSettings
    {
        public bool Enabled { get; set; } = true;
        public string TeamsEndpoint {  get; set; } = string.Empty;
        public string TeamsGroupConversationId { get; set; } = string.Empty;
        public bool SendLogsToTeams { get; set; }
        public Dictionary<string, string> AgentConversationIds { get; set; } = new Dictionary<string, string>();
        public bool UseTeamsChannel { get; set; }
        public string CreateTeamsChannelPostUrl { get; set; } = string.Empty;
        public string ReplyToTeamsChannelPostUrl { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string ChannelId { get; set; } = string.Empty;
    }
}

