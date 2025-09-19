// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Configuration
{
    public class MCPSettings
    {
        /// <summary>
        /// The list of MCP servers that will be exposed as individual subagents
        /// </summary>
        public HashSet<string> IsolatedServers { get; set; } = [];

        /// <summary>
        /// The list of MCP servers that will have their tools exposed to all subagents
        /// </summary>
        public HashSet<string> SharedServers { get; set; } = [];

        /// <summary>
        /// The list of STDIO-based MCP connections
        /// </summary>
        public List<MCPStdioConnectionSettings> StdioConnections { get; set; } = [];

        public int PingIntervalInSeconds { get; set; } = 60;
        public int PingTimeoutInSeconds { get; set; } = 10;
    }

    public class MCPStdioConnectionSettings
    {
        /// <summary>
        /// Name of the MCP connection
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Command to execute for the STDIO transport
        /// </summary>
        public string Command { get; set; } = string.Empty;

        /// <summary>
        /// Arguments to pass to the command
        /// </summary>
        public string[] Arguments { get; set; } = [];

        /// <summary>
        /// Whether this connection is enabled
        /// </summary>
        public bool Enabled { get; set; } = true;
    }
}
