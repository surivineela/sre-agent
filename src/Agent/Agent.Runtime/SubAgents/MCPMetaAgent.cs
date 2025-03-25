using AdaptiveCards.Rendering;
using Agent.Core.Models;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models;
using ModelContextProtocol.Client;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Agent.Runtime.SubAgents
{
    public class MCPMetaAgent : SubAgent, IMcpConnectable
    {
        public override string SystemPrompt { get; protected set; } = $@"You must delegate to another MCP server.
MCP stands for Model Context Protocol and represents a server which exposes prompts, tools, and resources to an LLM.";
        
        protected IMcpClient _mcpClient { get; set; }

        private ILoggerFactory _loggerFactory { get; }
        private ILogger _logger { get; }
        private ConcurrentDictionary<McpConnection, MCPAgent> _agents = new ConcurrentDictionary<McpConnection, MCPAgent>();
        private ConcurrentDictionary<McpConnection, AITool> _tools = new ConcurrentDictionary<McpConnection, AITool>();

        public MCPMetaAgent(IChatClient chatClient, ILoggerFactory loggerFactory) : base("MCPMetaAgent", chatClient)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger(typeof(MCPMetaAgent).FullName!);
        }

        public override IList<AITool> Tools()
        {
            return _tools.Values.ToList();
        }

        /// <inheritdoc />
        public void TryAddServer(McpConnection connection)
        {
            MCPAgent agent = new MCPAgent(connection, _chatClient) { Id = connection.Id, ClientName = connection.Id };

            bool successful = _agents.TryAdd(connection, agent);

            if (!successful)
            {
                throw new Exception("Agent has already been added.");
            }

            // Register a tool to call this agent with
            _tools[connection] = AIFunctionFactory.Create(
                agent.Ask,
                new()
                {
                    Name = $"call_{connection.Id}",
                    Description = $"Delegate to the server with the following system prompt: {agent.SystemPrompt}"
                }
            );

            ChatHistory.Add(new(ChatRole.Assistant, $"Added connection to {connection.Url}"));
        }

        /// <inheritdoc />
        public void TryRemoveServer(McpConnection connection)
        {

            _tools.TryRemove(connection, out _);
            _agents.TryRemove(connection, out _);
            ChatHistory.Add(new(ChatRole.Assistant, $"Removed connection to {connection.Url} due to connection failure"));
        }
    }
}
