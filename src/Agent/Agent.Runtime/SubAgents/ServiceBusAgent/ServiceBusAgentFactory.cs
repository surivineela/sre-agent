// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Linq.Expressions;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.Communication;
using Microsoft.DurableTask.Client;

namespace Agent.Runtime.SubAgents.ServiceBusAgent
{
    public class ServiceBusAgentFactory : SimpleResourceSubAgentFactoryBase<ServiceBusAgent, ServiceBusAgentInput, ServiceBusAgentActivity, ServiceBusAgentActivityInput>
    {
        private readonly IRemediationPlugin remediationPlugin;
        private readonly IRecordActionsPlugin recordActionsPlugin;

        public ServiceBusAgentFactory(
            IRemediationPlugin remediationPlugin,
            IRecordActionsPlugin recordActionsPlugin,
            IThreadOrchestrationManager mappingManager,
            IToolsRepository toolsRepository,
            DurableTaskClient durableTaskClient
            )
            : base(toolsRepository, mappingManager, durableTaskClient)
        {
            this.remediationPlugin = remediationPlugin;
            this.recordActionsPlugin = recordActionsPlugin;
        }

        protected override IEnumerable<Expression<Func<Delegate>>> GetToolList()
        {
            var remediationPluginDefinition = new RemediationPluginDefinition(remediationPlugin);
            yield return () => remediationPluginDefinition.ServiceBusSetLocalAuthSupport;

            var recordActionsPluginDefinition = new RecordActionsPluginDefinition(recordActionsPlugin);
            yield return () => recordActionsPluginDefinition.RecordAction;
            yield return () => recordActionsPluginDefinition.GetActionDetails;

            var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
            yield return () => controlFlowPluginDefinition.Wait;
            yield return () => controlFlowPluginDefinition.MarkPlanComplete;
            yield return () => controlFlowPluginDefinition.NotifyUser;
            yield return () => controlFlowPluginDefinition.AskUserForInput;
        }
    }
}

