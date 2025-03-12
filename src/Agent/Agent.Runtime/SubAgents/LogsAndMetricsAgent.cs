// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Plugins;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents
{
    public class LogsAndMetricsAgent : SubAgent
    {
        private ILogger<LogsAndMetricsAgent> _logger { get; }

        protected IMetricsPlugin _metricsPlugin { get; }

        public override string SystemPrompt { get; protected set; } = $@"You have a bunch of tools at your disposal. Do your best to use them to satisfy the user's ask.";

        public LogsAndMetricsAgent(IMetricsPlugin metricsPlugin, IChatClient chatClient, ILogger<LogsAndMetricsAgent> logger) : base("LogsAndMetricsAgent",chatClient)
        {
            _logger = logger;
            _metricsPlugin = metricsPlugin;
        }

        public override IList<AITool> Tools()
        {
            return new List<AITool>
            {
                AIFunctionFactory.Create(_metricsPlugin.GetFunctionAppRequestAvailability),
                AIFunctionFactory.Create(_metricsPlugin.GetWebAppCpuMetrics),
                AIFunctionFactory.Create(_metricsPlugin.GetMemoryMetrics),
                AIFunctionFactory.Create(_metricsPlugin.GetSuccessfulRequestVolumeAsync),
            };
        }
    }
}
