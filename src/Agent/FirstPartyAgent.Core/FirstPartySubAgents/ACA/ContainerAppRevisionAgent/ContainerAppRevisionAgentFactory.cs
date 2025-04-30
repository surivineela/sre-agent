// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.Communication;
using Agent.Runtime.SubAgents;
using FirstPartyAgent.Core.Plugins.Definitions;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;
using FirstPartyAgent.Plugins.Definitions;
using Microsoft.DurableTask.Client;
using System.Linq.Expressions;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.RevisionAgent
{
    // [MENDATORY]
    public class ContainerAppRevisionAgentFactory : SimpleResourceSubAgentFactoryBase<ContainerAppRevisionAgent, RevisionAgentInput, ContainerAppRevisionAgentActivity, ContainerAppRevisionAgentActivityInput>
    {
       
        private readonly IContainerAppRevisionPlugin _revisionPlugin;
        private readonly IKustoPlugin _kustoPlugin;

        public ContainerAppRevisionAgentFactory(
            IContainerAppRevisionPlugin revisionPlugin,
            IThreadOrchestrationManager mappingManager,
            IToolsRepository toolsRepository,
            DurableTaskClient durableTaskClient,
            IKustoPlugin kustoPlugin
            )
            : base(toolsRepository, mappingManager, durableTaskClient)
        {
            _revisionPlugin = revisionPlugin;
            _kustoPlugin = kustoPlugin;
        }

        protected override IEnumerable<Expression<Func<Delegate>>> GetToolList()
        {

           var kustoDefinition = new KustoPluginDefinition(_kustoPlugin);

            //// Register all Kusto functions dynamically
            //foreach (var pair in kustoDefinition.GetRegisteredFunctionDelegates())
            //{
            //    var functionName = pair.Key;
            //    var func = pair.Value;
            //    var f = new Func<Task<string>>(func);
                
            //    // Expression wrapper for Func<Task<string>> to Delegate
            //    yield return () => new Func<Task<string>>(func);
            //}

            // Add static methods
            yield return () => kustoDefinition.ListFunctionsAsync;
            yield return () => kustoDefinition.ExecuteFunction;
            yield return () => kustoDefinition.ExecuteKustoQuery;

            // Import all tools that defined in System prompt of this 'RevisionAgent' sub-agent including required fundamental plugins like RecordActionsPluginDefinition, ControlFlowPluginDefinition, ApprovalPluginDefinition, etc.
            var revisionPluginDefinition = new ContainerAppRevisionPluginDefinition(_revisionPlugin);
            yield return () => revisionPluginDefinition.ListRevisions;
            yield return () => revisionPluginDefinition.GetRevisionTrafficWithReplicaCount;
            yield return () => revisionPluginDefinition.GetActiveRevisionSessions;
            yield return () => revisionPluginDefinition.GetHpaHeartbeatMetrics;
            yield return () => revisionPluginDefinition.GetRevisionSpecChanges;
            yield return () => revisionPluginDefinition.GetEventProcessorEventsWithoutReplica;
            yield return () => revisionPluginDefinition.GetPodHeartbeatStatus;
            yield return () => revisionPluginDefinition.GetInternalEventProcessorEventsForPod;

            yield return () => revisionPluginDefinition.GetReplicaCount;
            yield return () => revisionPluginDefinition.ContainerAppRevisionStatus;

            var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
            yield return () => controlFlowPluginDefinition.Wait;
            yield return () => controlFlowPluginDefinition.MarkPlanComplete;
            yield return () => controlFlowPluginDefinition.NotifyUser;
            yield return () => controlFlowPluginDefinition.AskUserForInput;

            //var approvalPluginDefinition = new ApprovalPluginDefinition(_approvalPlugin);
            //yield return () => approvalPluginDefinition.StartApprovalFlow;
        }
    }
}
