using Agent.Core.Models;
using McpDotNet.Client;
using McpDotNet.Configuration;
using McpDotNet.Extensions.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agent.Runtime.SubAgents
{
    public class MCPAgent : SubAgent
    {
        public required string Id { get; init; }
        public required string ClientName { get; init; }
        public override string SystemPrompt { get; protected set; } = $@"Your tools are loaded from an MCP server. Choose the best tool available.";
        public IMcpClient MCPClient { get; set; }

        protected GraphDBQueryAgent _queryAgent { get; }

        private ILoggerFactory _loggerFactory;
        private ILogger _logger;
        private IList<AITool> _tools = new List<AITool>();

        public MCPAgent(IChatClient chatClient, ILoggerFactory loggerFactory) : base("MCPAgent", chatClient)
        {
            _loggerFactory = loggerFactory;
        }

        public async Task Initialize(string url)
        {
            _logger = _loggerFactory.CreateLogger($"{typeof(MCPMetaAgent).FullName!}.{ClientName}");

            McpClientOptions options = new()
            {
                ClientInfo = new() { Name = Id, Version = "1.0.0" }
            };

            var config = new McpServerConfig
            {
                Id = Id,
                Name = ClientName,
                TransportType = "sse",
                Location = url
            };

            var factory = new McpClientFactory(
                [config],
                options,
                NullLoggerFactory.Instance
            );

            _logger.LogInformation("Attempting to connect to {endpoint}", url);

            // Can't use McpSessionScope yet because we need lower level functionality for pinging
            MCPClient = await factory.GetClientAsync(Id);
            var tools = await MCPClient.ListToolsAsync();
            _tools = tools.Tools.Select(t => t.ToAITool(MCPClient)).ToList();

            if (!string.IsNullOrEmpty(MCPClient.ServerInstructions))
            {
                SystemPrompt = MCPClient.ServerInstructions;
            }
        }

        public override IList<AITool> Tools()
        {
            return _tools;
        }
    }
}
