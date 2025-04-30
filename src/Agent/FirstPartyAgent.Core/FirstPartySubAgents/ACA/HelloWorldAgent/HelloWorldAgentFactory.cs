// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Linq.Expressions;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.Communication;
using Agent.Runtime.SubAgents;
using FirstPartyAgent.Core.Plugins.Definitions;
using FirstPartyAgent.Core.Plugins.Interfaces;
using Microsoft.DurableTask.Client;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.HelloWorldAgent
{
    // [MENDATORY]
    public class HelloWorldAgentFactory : SimpleResourceSubAgentFactoryBase<HelloWorldAgent, HelloWorldAgentInput, HelloWorldAgentActivity, HelloWorldAgentActivityInput>
    {
        
        private readonly lHelloWorldPlugin _helloWorldPlugin;

        public HelloWorldAgentFactory(
            lHelloWorldPlugin helloWorldPlugin,
            IThreadOrchestrationManager mappingManager,
            IToolsRepository toolsRepository,
            DurableTaskClient durableTaskClient
            )
            : base(toolsRepository, mappingManager, durableTaskClient)
        {
            _helloWorldPlugin = helloWorldPlugin;
        }

        protected override IEnumerable<Expression<Func<Delegate>>> GetToolList()
        {
            // Import all tools that defined in System prompt of this 'HelloWorldAgent' sub-agent including required fundamental plugins like RecordActionsPluginDefinition, ControlFlowPluginDefinition, ApprovalPluginDefinition, etc.

            var helloWorldPluginDefinition = new HelloWorldPluginDefinition(_helloWorldPlugin);
            yield return () => helloWorldPluginDefinition.GetHelloWorldMessageAsync;

            var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
            yield return () => controlFlowPluginDefinition.Wait;
            yield return () => controlFlowPluginDefinition.MarkPlanComplete;
            yield return () => controlFlowPluginDefinition.NotifyUser;
            yield return () => controlFlowPluginDefinition.AskUserForInput;
        }
    }
}
