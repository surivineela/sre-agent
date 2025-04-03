// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Runtime.Models;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents
{
    public class MCPAgent : SubAgent
    {
        public required string Id { get; init; }
        public required string ClientName { get; init; }
        public override string SystemPrompt { get; protected set; } = $@"Your tools are loaded from an MCP server. Choose the best tool available.";

        protected GraphDBQueryAgent _queryAgent { get; }

        private McpConnection _mcpConnection;
        private IList<AITool> _tools;

        public MCPAgent(McpConnection mcpConnection, IChatClient chatClient) : base("MCPAgent", chatClient)
        {
            _mcpConnection = mcpConnection;
            _tools = _mcpConnection.Tools;
        }

        public override IList<AITool> Tools()
        {
            return _tools;
        }
    }
}

