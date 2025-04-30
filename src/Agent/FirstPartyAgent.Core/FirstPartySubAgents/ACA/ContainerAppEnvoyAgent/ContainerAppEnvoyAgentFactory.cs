// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins;
using Agent.Runtime.Communication;
using Agent.Runtime.SubAgents;
using FirstPartyAgent.Core.Plugins.Definitions;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;
using FirstPartyAgent.Plugins.Definitions;
using Microsoft.DurableTask.Client;
using System.Linq.Expressions;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppEnvoyAgent
{
    // [MENDATORY]
    public class ContainerAppEnvoyAgentFactory : SimpleResourceSubAgentFactoryBase<ContainerAppEnvoyAgent, ContainerAppEnvoyAgentInput, ContainerAppEnvoyAgentActivity, ContainerAppEnvoyAgentActivityInput>
    {
        private readonly IContainerAppEnvoyPlugin _envoyPlugin;
        private readonly IKustoPlugin _kustoPlugin;

        public ContainerAppEnvoyAgentFactory(
            IContainerAppEnvoyPlugin envoyPlugin,
            IThreadOrchestrationManager mappingManager,
            IToolsRepository toolsRepository,
            DurableTaskClient durableTaskClient,
            IKustoPlugin kustoPlugin
            )
            : base(toolsRepository, mappingManager, durableTaskClient)
        {
            _envoyPlugin = envoyPlugin;
            _kustoPlugin = kustoPlugin;
        }

        protected override IEnumerable<Expression<Func<Delegate>>> GetToolList()
        {

            var kustoDefinition = new KustoPluginDefinition(_kustoPlugin);
            // Add static methods
            yield return () => kustoDefinition.ListFunctionsAsync;
            yield return () => kustoDefinition.ExecuteFunction;
            yield return () => kustoDefinition.ExecuteKustoQuery;


            var envoyPluginDefinition = new ContainerAppEnvoyPluginDefinition(_envoyPlugin);
            yield return () => envoyPluginDefinition.GetEnvoyAbnormalLogs;
            yield return () => envoyPluginDefinition.GetEnvoyControllerLogs;
            yield return () => envoyPluginDefinition.GetEnvoyAccessLogs;
            yield return () => envoyPluginDefinition.GetSwiftNetworkingEvents;

            var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
            yield return () => controlFlowPluginDefinition.Wait;
            yield return () => controlFlowPluginDefinition.MarkPlanComplete;
            yield return () => controlFlowPluginDefinition.NotifyUser;
            yield return () => controlFlowPluginDefinition.AskUserForInput;
        }
    }
}
