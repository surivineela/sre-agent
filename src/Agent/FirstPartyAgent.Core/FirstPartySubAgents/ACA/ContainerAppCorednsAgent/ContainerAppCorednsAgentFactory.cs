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

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.CorednsAgent
{
    // [MENDATORY]
    public class ContainerAppCorednsAgentFactory : SimpleResourceSubAgentFactoryBase<ContainerAppCorednsAgent, CorednsAgentInput, ContainerAppCorednsAgentActivity, ContainerAppCorednsAgentActivityInput>
    {
        private readonly IContainerAppCorednsPlugin _CorednsPlugin;
        private readonly IKustoPlugin _kustoPlugin;

        public ContainerAppCorednsAgentFactory(
            IContainerAppCorednsPlugin CorednsPlugin,
            IThreadOrchestrationManager mappingManager,
            IToolsRepository toolsRepository,
            DurableTaskClient durableTaskClient,
            IKustoPlugin kustoPlugin
            )
            : base(toolsRepository, mappingManager, durableTaskClient)
        {
            _CorednsPlugin = CorednsPlugin;
            _kustoPlugin = kustoPlugin;
        }

        protected override IEnumerable<Expression<Func<Delegate>>> GetToolList()
        {

           //var kustoDefinition = new KustoPluginDefinition(_kustoPlugin);
           // // Add static methods
           // yield return () => kustoDefinition.ListFunctionsAsync;
           // yield return () => kustoDefinition.ExecuteFunction;
           // yield return () => kustoDefinition.ExecuteKustoQuery;

            // Import all tools that defined in System prompt of this 'CorednsAgent' sub-agent including required fundamental plugins like RecordActionsPluginDefinition, ControlFlowPluginDefinition, ApprovalPluginDefinition, etc.
            var CorednsPluginDefinition = new ContainerAppCorednsPluginDefinition(_CorednsPlugin);
            yield return () => CorednsPluginDefinition.CheckIfCustomDNSConfigured;
            yield return () => CorednsPluginDefinition.GetCustomDNSServers;
            yield return () => CorednsPluginDefinition.GetUpstreamCustomDNSServerHealthStatus;
            yield return () => CorednsPluginDefinition.GetCoreDNSConfigReloadFailuresCount;
            yield return () => CorednsPluginDefinition.GetCoreDNSTotalDNSRequestCount;
            yield return () => CorednsPluginDefinition.GetCoreDNSForwardConcurrentRejectsCount;
            yield return () => CorednsPluginDefinition.GetAverageLatencyOfDNSResolutionRequests;
            yield return () => CorednsPluginDefinition.GetAverageLatencyOfCoreDNSKubernetesDNSProgramming;
            yield return () => CorednsPluginDefinition.GetAverageLatencyOfUpstreamDNSResolutionForwardRequests;
            yield return () => CorednsPluginDefinition.GetCorednsPodFailureEvents;
            yield return () => CorednsPluginDefinition.GetSwiftBootstrapAgentPodFailureEvents;
            yield return () => CorednsPluginDefinition.GetSwiftBootstrapAgentPodHealthStatus;
            yield return () => CorednsPluginDefinition.GetDNSConfigUpdateStatus;
            yield return () => CorednsPluginDefinition.CheckIfDNSServerFailedToResolveDot;


            var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
            yield return () => controlFlowPluginDefinition.Wait;
            yield return () => controlFlowPluginDefinition.MarkPlanComplete;
            yield return () => controlFlowPluginDefinition.NotifyUser;
            yield return () => controlFlowPluginDefinition.AskUserForInput;
        }
    }
}
