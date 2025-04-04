using Agent.Core.Models;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace Agent.Runtime.SubAgents
{
    public class ReliabilityAgent: SubAgent
    {
        private ILogger<ReliabilityAgent> _logger { get; }

        protected IReliabilityPlugin _reliabilityPlugin { get; }

        protected GraphDBQueryAgent _graphAgent { get; }

        public override string SystemPrompt { get; protected set; } =
            $@"You have a bunch of tools at your disposal. Do your best to use them to satisfy the user's ask.";

        public ReliabilityAgent(
            IReliabilityPlugin reliabilityPlugin,
            GraphDBQueryAgent graphAgent,
            IChatClient chatClient,
            ILogger<ReliabilityAgent> logger
        )
            : base("ReliabilityAgent", chatClient)
        {
            _logger = logger;
            _reliabilityPlugin = reliabilityPlugin;
            _graphAgent = graphAgent;
        }

        public override IList<AITool> Tools()
        {
            return new List<AITool>
            {
                AIFunctionFactory.Create(_reliabilityPlugin.GetReliabilityStatus),
                AIFunctionFactory.Create(_reliabilityPlugin.GetReliabilityStatusForSubscriptions),
                AIFunctionFactory.Create(_reliabilityPlugin.UpdateAlwaysOn),
                AIFunctionFactory.Create(_reliabilityPlugin.UpdateHealthCheck),
                AIFunctionFactory.Create(_reliabilityPlugin.UpdateAutoHeal),
                AIFunctionFactory.Create(_reliabilityPlugin.UpdateHostWorkers),
                AIFunctionFactory.Create(this.LaunchGraphTraversalAgentAsync)
            };
        }

        [KernelFunction("launch_graph_traversal_agent")]
        [Description("This agent will convert a specific question about a service's azure resources and attempt answer it using graph queries. It will also find the relevant resource within the graph.")]
        public async Task<string> LaunchGraphTraversalAgentAsync(string question)
        {
            _logger.LogInformation("Invoking graph traversal agent");
            string answer = await _graphAgent.Ask(question);
            _logger.LogInformation($"Graph traversal agent responded with: {answer}");
            return answer;
        }

        [KernelFunction("scan_all_apps_reliability")]
        [Description("Scans for the apps' reliability and resilience. It sees how optimal the user's apps are.")]
        public async Task<string> Scan(
           [Description("Keep null")] CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Invoked scan_all_apps_reliability function");
            try
            {
                var reliabilityTable = await _reliabilityPlugin.GetReliabilityStatusForSubscriptions(cancellationToken);
                return reliabilityTable;
            }
            catch (Exception)
            {
                throw;
            }
        }

        [KernelFunction("scan_all_reliable_apps_to_update")]
        [Description("Scans for the apps' reliability and resilience. It sees how optimal the user's apps are so that we can later modify them")]
        public async Task<string> ScanReliableApps(
           [Description("Keep null")] CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Invoked scan_all_reliable_apps_to_update function");
            try
            {
                var reliableApps = await _reliabilityPlugin.GetAppsToMonitor(cancellationToken);
                return reliableApps;
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
