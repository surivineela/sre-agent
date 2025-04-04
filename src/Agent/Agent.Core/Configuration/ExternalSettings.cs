// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------


namespace Agent.Core.Configuration
{
    public class ExternalSettings
    {
        public string TeamsEndpoint { get; set; } = string.Empty;
        public TeamsBotSettings TeamsBot { get; set; } = new();
        public GitHubSettings GitHub { get; set; } = new();
        public MCPSettings MCP { get; set; } = new();
        public DashboardSettings Dashboard { get; set; } = new();
    }
}

