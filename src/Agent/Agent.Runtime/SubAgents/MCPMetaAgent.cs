using Agent.Core.Models;
using McpDotNet.Client;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Agent.Runtime.SubAgents
{
    public class MCPMetaAgent : SubAgent
    {
        public override string SystemPrompt { get; protected set; } = $@"You must delegate to another MCP server.
MCP stands for Model Context Protocol and represents a server which exposes prompts, tools, and resources to an LLM.";
        
        protected IMcpClient _mcpClient { get; set; }

        private ILoggerFactory _loggerFactory { get; }
        private ILogger _logger { get; }
        private ConcurrentDictionary<string, MCPAgent> _agents = new ConcurrentDictionary<string, MCPAgent>();
        private ConcurrentDictionary<string, AITool> _tools = new ConcurrentDictionary<string, AITool>();

        public MCPMetaAgent(IChatClient chatClient, ILoggerFactory loggerFactory) : base("MCPMetaAgent", chatClient)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger(typeof(MCPMetaAgent).FullName!);
        }

        public override IList<AITool> Tools()
        {
            return _tools.Values.ToList();
        }

        public async Task<string> AddServer(string key, string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                throw new ArgumentException("The provided string is not a valid URI.", nameof(url));
            }

            try
            {
                MCPAgent agent = new MCPAgent(_chatClient, _loggerFactory) { Id = key, ClientName = key };
                await agent.Initialize(url);

                bool successful = _agents.TryAdd(key, agent);

                if (!successful)
                {
                    throw new Exception("Agent has already been added.");
                }

                // Register a tool to call this agent with
                _tools[key] = AIFunctionFactory.Create(
                    agent.Ask,
                    new()
                    {
                        Name = $"call_{key}",
                        Description = $"Delegate to the server with the following system prompt: {agent.SystemPrompt}"
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to add server at {url}");
                throw;
            }

            return key;
        }

        public void TryRemoveServer(string key)
        {
            _tools.TryRemove(key, out _);
            _agents.TryRemove(key, out _);
        }

        public bool TryGetAgent(string key, out MCPAgent? agent)
        {
            return _agents.TryGetValue(key, out agent);
        }
    }
}
