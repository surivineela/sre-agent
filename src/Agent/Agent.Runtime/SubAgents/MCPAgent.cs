// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Framework;
using Agent.Runtime.Models;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents
{
    public class MCPAgent : SubAgent
    {
        public required string Id { get; init; }
        public required string ClientName { get; init; }
        public override string SystemPrompt { get; protected set; } = $@"Your tools are loaded from an MCP server. Choose the best tool available.";

        private McpConnection _mcpConnection;
        private IList<AITool> _tools;

        public MCPAgent(McpConnection mcpConnection, IChatClientProvider chatClientProvider)
            : base("MCPAgent", chatClientProvider)
        {
            _mcpConnection = mcpConnection;
            _tools = _mcpConnection.Tools ?? new List<AITool>();
        }

        public override IList<AITool> Tools()
        {
            return _tools;
        }

        public override Task<IList<Microsoft.Extensions.AI.ChatMessage>> GetStartingMessagesAsync()
        {
            throw new NotImplementedException();
        }
    }
}

