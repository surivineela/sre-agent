// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Plugins;
using Agent.Plugins.Interface;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents
{
    public class LogsAndMetricsAgent : SubAgent
    {
        private ILogger<LogsAndMetricsAgent> _logger { get; }

        protected IMetricsPlugin _metricsPlugin { get; }

        public override string SystemPrompt { get; protected set; } = $@"You have a bunch of tools at your disposal. Do your best to use them to satisfy the user's ask.";

        public LogsAndMetricsAgent(IMetricsPlugin metricsPlugin, IChatClient chatClient, ILogger<LogsAndMetricsAgent> logger)
            : base("LogsAndMetricsAgent", chatClient)
        {
            _logger = logger;
            _metricsPlugin = metricsPlugin;
        }

        public override IList<AITool> Tools()
        {
            var metricsPluginDefinition = new MetricsPluginDefinition(_metricsPlugin);
            return new List<AITool>
            {
                AIFunctionFactory.Create(metricsPluginDefinition.GetFunctionAppRequestAvailability),
                AIFunctionFactory.Create(metricsPluginDefinition.GetWebAppCpuMetrics),
                AIFunctionFactory.Create(metricsPluginDefinition.GetMemoryMetrics),
                AIFunctionFactory.Create(metricsPluginDefinition.GetSuccessfulRequestVolumeAsync),
            };
        }

        public override Task<IList<Microsoft.Extensions.AI.ChatMessage>> GetStartingMessagesAsync()
        {
            throw new NotImplementedException();
        }
    }
}
