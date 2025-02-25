// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents
{
    public class GenericAgent : SubAgent
    {
        private ILogger<GenericAgent> _logger { get; }

        protected ISubscriptionPlugin _subscriptionPlugin { get; }

        protected ITimePlugin _timePlugin { get; }

        protected IMonitorPlugin _monitorPlugin { get; }

        protected override string SystemPrompt { get; } = $@"You have a bunch of tools at your disposal. Do your best to use them to satisfy the user's ask.";

        public GenericAgent(ISubscriptionPlugin subscriptionPlugin, ITimePlugin timePlugin, IMonitorPlugin monitorPlugin, IChatClient chatClient, ILogger<GenericAgent> logger) : base("GenericAgent", chatClient)
        {
            _logger = logger;
            _subscriptionPlugin = subscriptionPlugin;
            _timePlugin = timePlugin;
            _monitorPlugin = monitorPlugin;
        }

        public override IList<AITool> Tools()
        {
            return new List<AITool>
            {
                AIFunctionFactory.Create(_subscriptionPlugin.ListAllSubscriptionsAsync),
                AIFunctionFactory.Create(_subscriptionPlugin.ListAppServicesAsync),

                AIFunctionFactory.Create(_monitorPlugin.StartMonitor),
                AIFunctionFactory.Create(_monitorPlugin.UpdateMonitorInterval),
                AIFunctionFactory.Create(_monitorPlugin.StopMonitor),
                AIFunctionFactory.Create(_monitorPlugin.GetMonitorInfo),
                AIFunctionFactory.Create(_monitorPlugin.SummarizeMonitorActivity),

                AIFunctionFactory.Create(_timePlugin.GetCurrentUtcTime),
            };
        }
    }
}
