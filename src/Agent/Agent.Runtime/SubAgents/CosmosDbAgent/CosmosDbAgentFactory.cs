// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core;
using Agent.Core.Models.Api.v1;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.Communication;
using Agent.Runtime.SubAgents.AppServiceRemediation;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using System.Linq.Expressions;
using System.Text.Json;

namespace Agent.Runtime.SubAgents.CosmosDbAgent
{
    public class CosmosDbAgentFactory : SimpleResourceSubAgentFactoryBase<CosmosDbAgent, CosmosDbAgentInput, CosmosDbAgentActivity, CosmosDbAgentActivityInput>
    {
        private readonly IApprovalPlugin approvalPlugin;
        private readonly IRemediationPlugin remediationPlugin;
        private readonly IRecordActionsPlugin recordActionsPlugin;

        public CosmosDbAgentFactory(
            IApprovalPlugin approvalPlugin,
            IRemediationPlugin remediationPlugin,
            IRecordActionsPlugin recordActionsPlugin,
            IThreadOrchestrationManager mappingManager,
            ToolsRepository toolsRepository,
            DurableTaskClient durableTaskClient
            )
            : base(toolsRepository, mappingManager, durableTaskClient)
        {
            this.approvalPlugin = approvalPlugin;
            this.remediationPlugin = remediationPlugin;
            this.recordActionsPlugin = recordActionsPlugin;
        }

        protected override IEnumerable<Expression<Func<Delegate>>> GetToolList()
        {
            var remediationPluginDefinition = new RemediationPluginDefinition(remediationPlugin);
            yield return () => remediationPluginDefinition.CosmosDbSetLocalAuthSupport;

            var recordActionsPluginDefinition = new RecordActionsPluginDefinition(recordActionsPlugin);
            yield return () => recordActionsPluginDefinition.RecordAction;
            yield return () => recordActionsPluginDefinition.GetActionDetails;

            var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
            yield return () => controlFlowPluginDefinition.Wait;
            yield return () => controlFlowPluginDefinition.MarkPlanComplete;
            yield return () => controlFlowPluginDefinition.NotifyUser;
            yield return () => controlFlowPluginDefinition.AskUserForInput;

            var approvalPluginDefinition = new ApprovalPluginDefinition(approvalPlugin);
            //yield return () => approvalPluginDefinition.StartApprovalFlow;
        }
    }
}

