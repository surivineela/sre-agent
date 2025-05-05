// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Linq.Expressions;
using System.Text.Json;
using Agent.Core;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime;
using Agent.Runtime.Communication;
using Agent.Runtime.SubAgents;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.RevisionAgent;
using FirstPartyAgent.Core.Plugins.Definitions;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;
using FirstPartyAgent.Plugins.Definitions;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppEnvoyAgent
{
    // [MENDATORY]
    public class ContainerAppEnvoyAgentFactory 
    {
        private readonly IContainerAppEnvoyPlugin _envoyPlugin;
        private readonly IKustoPlugin _kustoPlugin;
        private readonly AgentToolsRegistry _toolsRegistry;
        private readonly DurableTaskClient _durableTaskClient;
        private readonly IThreadOrchestrationManager _mappingManager;
        public const string OrchestrationInstanceIdPrefix = nameof(ContainerAppEnvoyAgentInput);
        public ContainerAppEnvoyAgentFactory(
            IContainerAppEnvoyPlugin envoyPlugin,
            IThreadOrchestrationManager mappingManager,
            IToolsRepository toolsRepository,
            DurableTaskClient durableTaskClient,
            IKustoPlugin kustoPlugin
            )
            
        {
            _envoyPlugin = envoyPlugin;
            _kustoPlugin = kustoPlugin;
            _toolsRegistry = new AgentToolsRegistry();
            _mappingManager = mappingManager;
            //_toolsRegistry.RegisterTool<MetricsPluginDefinition>(x => x.GetSuccessfulRequestVolumeAsync);
            //_toolsRegistry.RegisterTool<ArmPluginDefinition>(x => x.SetMinimumTlsVersion);
            _toolsRegistry.RegisterPlugin<RecordActionsPluginDefinition>();
            _toolsRegistry.RegisterPlugin<ControlFlowPluginDefinition>();
            _toolsRegistry.RegisterPlugin<ContainerAppRevisionPluginDefinition>();
            
            _durableTaskClient = durableTaskClient;
            


        }

        public async Task<string> StartOrchestration(
           ContainerAppEnvoyAgentActivityInput input,
           Guid threadId)
        {
            var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";

            await _mappingManager.AddMappingAsync(threadId.ToString(), instanceId);

            await _durableTaskClient.ScheduleNewContainerAppEnvoyAgentInstanceAsync(
                new ContainerAppEnvoyAgentInput(
                    Input: input,
                    ToolSignatures: _toolsRegistry.ToolSignatures,
                    ThreadId: threadId),
                new StartOrchestrationOptions(InstanceId: instanceId));

            return instanceId;
        }

        public ContainerAppEnvoyAgentActivityInput DeserializeInput(string serializedOrchestrationInput)
        {
            return JsonSerializer.Deserialize<ContainerAppEnvoyAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
        }
    }
}
