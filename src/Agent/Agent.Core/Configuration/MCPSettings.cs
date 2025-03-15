using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public int PingIntervalInSeconds { get; set; } = 60;
        public int PingTimeoutInSeconds { get; set; } = 10;
    }
}
