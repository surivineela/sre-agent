using Agent.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Runtime.SubAgents
{
    public class ArchitectureAgent : SubAgent
    {
        private ILogger<ArchitectureAgent> _logger { get; }

        protected GraphDBQueryAgent _queryAgent { get; }

        protected override string SystemPrompt { get; } = $@"Use the tools at your disposal to answer questions relating to the architecture of a services.

If asked about bad practices, and example that you should search for is not using managed identity to talk to SQL if a webapp is associated with a Sql server.
To do so, confirm:
1) These apps talk to SQL
2) These apps do not have a managed identity associated with them

You can run multiple tool calls to answer a single question.";

        public ArchitectureAgent(GraphDBQueryAgent queryAgent, IChatClient chatClient, ILogger<ArchitectureAgent> logger) : base(chatClient)
        {
            _logger = logger;
            _queryAgent = queryAgent;
        }

        public override IList<AITool> Tools()
        {
            return [
                AIFunctionFactory.Create(this.LaunchGraphTraversalAgentAsync),
            ];
        }

        [KernelFunction("launch_graph_traversal_agent")]
        [Description("This agent will convert a specific question about a service's azure resources and attempt answer it using graph queries.")]
        public async Task<string> LaunchGraphTraversalAgentAsync(string question)
        {
            _logger.LogInformation("Invoking graph traversal agent");
            string answer = await _queryAgent.Ask(question);
            _logger.LogInformation($"Graph traversal agent responded with: {answer}");
            return answer;
        }
    }
}
