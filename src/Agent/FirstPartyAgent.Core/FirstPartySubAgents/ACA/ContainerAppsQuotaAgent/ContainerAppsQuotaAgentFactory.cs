// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins;
using Agent.Runtime.Communication;
using Agent.Runtime.SubAgents;
using FirstPartyAgent.Plugins;
using FirstPartyAgent.Plugins.Definitions;
using Microsoft.DurableTask.Client;
using System.Linq.Expressions;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppsQuotaAgent
{
    // [MENDATORY]
    public class ContainerAppsQuotaAgentFactory : SimpleResourceSubAgentFactoryBase<ContainerAppsQuotaAgent, ContainerAppsQuotaAgentInput, ContainerAppsQuotaAgentActivity, ContainerAppsQuotaAgentActivityInput>
    {
        private readonly IIcmPlugin icmPlugin;
        private readonly IContainerAppsPlugin containerAppsPlugin;

        public ContainerAppsQuotaAgentFactory(
            ITimePlugin timePlugin,
            IIcmPlugin icmPlugin,
            IContainerAppsPlugin containerAppsPlugin,
            IToolsRepository toolsRepository,
            IThreadOrchestrationManager mappingManager,
            DurableTaskClient durableTaskClient
            )
            : base(toolsRepository, mappingManager, durableTaskClient)
        {
            this.icmPlugin = icmPlugin;
            this.containerAppsPlugin = containerAppsPlugin;
        }

        protected override IEnumerable<Expression<Func<Delegate>>> GetToolList()
        {
            var containerAppPluginDefinition = new ContainerAppsPluginDefinition(containerAppsPlugin);
            yield return () => containerAppPluginDefinition.ValidateQuotaRequest;
            yield return () => containerAppPluginDefinition.SetSubscriptionQuota;
            yield return () => containerAppPluginDefinition.GetSubscriptionDetail;
            yield return () => containerAppPluginDefinition.GetSubscriptionUsage;

            var icmPluginDefinition = new IcmPluginDefinition(icmPlugin);
            yield return () => icmPluginDefinition.GetIncidentInfo;
            yield return () => icmPluginDefinition.AddDiscussionEntry;
            yield return () => icmPluginDefinition.ResolveIncident;

            var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
            yield return () => controlFlowPluginDefinition.Wait;
            yield return () => controlFlowPluginDefinition.MarkPlanComplete;
            yield return () => controlFlowPluginDefinition.NotifyUser;
            yield return () => controlFlowPluginDefinition.AskUserForInput;
        }
    }
}
