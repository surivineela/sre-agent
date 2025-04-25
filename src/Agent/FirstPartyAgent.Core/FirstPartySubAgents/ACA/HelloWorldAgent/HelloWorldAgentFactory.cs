// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.Communication;
using Agent.Runtime.SubAgents;
using FirstPartyAgent.Core.Plugins.Interfaces;
using Microsoft.DurableTask.Client;
using System.Linq.Expressions;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.HelloWorldAgent
{
    // [MENDATORY]
    public class HelloWorldAgentFactory : SimpleResourceSubAgentFactoryBase<HelloWorldAgent, HelloWorldAgentInput, HelloWorldAgentActivity, HelloWorldAgentActivityInput>
    {

        public HelloWorldAgentFactory(
            lHelloWorldPlugin helloWorldPlugin,
            IThreadOrchestrationManager mappingManager,
            IToolsRepository toolsRepository,
            DurableTaskClient durableTaskClient
            )
            : base(toolsRepository, mappingManager, durableTaskClient)
        {
        }

        protected override IEnumerable<Expression<Func<Delegate>>> GetToolList()
        {
            // Import all tools that defined in System prompt of this 'HelloWorldAgent' sub-agent including required fundamental plugins like RecordActionsPluginDefinition, ControlFlowPluginDefinition, ApprovalPluginDefinition, etc.
            var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
            yield return () => controlFlowPluginDefinition.Wait;
            yield return () => controlFlowPluginDefinition.MarkPlanComplete;
            yield return () => controlFlowPluginDefinition.NotifyUser;
            yield return () => controlFlowPluginDefinition.AskUserForInput;
        }
    }
}

