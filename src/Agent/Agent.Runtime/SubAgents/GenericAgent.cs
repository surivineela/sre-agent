// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Graph.Crawler.ARM;
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

        protected ICurrentStatePlugin _currentStatePlugin { get; }

        protected ICodeAnalyzerPlugin _codeAnalyzerPlugin { get; }

        protected IRemediationPlugin _remediationPlugin { get; }

        protected ResourceGraphCrawler _crawler { get; }

        protected override string SystemPrompt { get; } = $@"You have a bunch of tools at your disposal. Do your best to use them to satisfy the user's ask.";

        public GenericAgent(
            ISubscriptionPlugin subscriptionPlugin,
            ITimePlugin timePlugin,
            ICodeAnalyzerPlugin codeAnalyzerPlugin,
            IMonitorPlugin monitorPlugin,
            ICurrentStatePlugin currentStatePlugin,
            IChatClient chatClient,
            IRemediationPlugin remediationPlugin,
            ResourceGraphCrawler crawler,
            ILogger<GenericAgent> logger)
            : base("GenericAgent", chatClient)
        {
            _logger = logger;
            _subscriptionPlugin = subscriptionPlugin;
            _timePlugin = timePlugin;
            _codeAnalyzerPlugin = codeAnalyzerPlugin;
            _monitorPlugin = monitorPlugin;
            _currentStatePlugin = currentStatePlugin;
            _remediationPlugin = remediationPlugin;
            _crawler = crawler;
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

                AIFunctionFactory.Create(_currentStatePlugin.GetCurrentAppState),
                AIFunctionFactory.Create(_currentStatePlugin.GetCurrentBotState),

                AIFunctionFactory.Create(_remediationPlugin.ScaleAppServicePlanVertically),
                AIFunctionFactory.Create(_remediationPlugin.RestartWebApp),
                AIFunctionFactory.Create(_remediationPlugin.CollectMemoryDump),
                AIFunctionFactory.Create(_remediationPlugin.CalculateScalingCost),
                AIFunctionFactory.Create(_remediationPlugin.SuggestNextSku),
            };
        }
    }
}