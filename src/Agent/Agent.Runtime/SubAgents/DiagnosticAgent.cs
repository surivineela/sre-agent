// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Plugins;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents
{
    public class DiagnosticAgent : SubAgent
    {
        private ILogger<DiagnosticAgent> _logger { get; }

        protected IDiagnosePlugin _diagnosePlugin { get; }

        public override string SystemPrompt { get; protected set; } =
            $@"You have a bunch of tools at your disposal to diagnose app services. Do your best to use them to satisfy the user's ask.";

        public DiagnosticAgent(
            IDiagnosePlugin diagnosePlugin,
            IChatClient chatClient,
            ILogger<DiagnosticAgent> logger
        )
            : base("DiagnosticAgent", chatClient)
        {
            _logger = logger;
            _diagnosePlugin = diagnosePlugin;
        }

        public override IList<AITool> Tools()
        {
            return new List<AITool>
            {
                AIFunctionFactory.Create(_diagnosePlugin.Diagnose),
                AIFunctionFactory.Create(_diagnosePlugin.GetDiagnoseStatus),
            };
        }
    }
}
