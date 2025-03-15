using Agent.Runtime.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Runtime.Interfaces
{
    public interface IMcpConnectable
    {
        /// <summary>
        /// Adds a MCP server to the list of active connections and completes the work associated with having access to that server.
        /// E.g. Creates an agent with the tools of that MCP server as a subagent, exposes those tools to specific agents, etc.
        /// </summary>
        /// <param name="connection"></param>
        public void TryAddServer(McpConnection connection);

        /// <summary>
        /// Removes a MCP server from the list of active connections and completes the work associated with having access to that server.
        /// E.g. Removes the agent with the tools of that MCP server as a subagent, removes the tools of that MCP server, etc.
        /// </summary>
        /// <param name="connection"></param>
        public void TryRemoveServer(McpConnection connection);
    }
}
