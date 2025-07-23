// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.TeamsPlugin
{
    public class TeamsClientSettings
    {
        public bool Enabled { get; set; } = true;
        public string? TeamsEndpoint { get; set; }
        public string? TeamsGroupConversationId { get; set; }
        public bool SendLogsToTeams { get; set; }
        public Dictionary<string, string> AgentConversationIds { get; set; } = new Dictionary<string, string>();
        public bool UseTeamsChannel { get; set; }
        public string? CreateTeamsChannelPostUrl { get; set; }
        public string? ReplyToTeamsChannelPostUrl { get; set; }
        public string? GroupId { get; set; }
        public string? ChannelId { get; set; }
    }
}

