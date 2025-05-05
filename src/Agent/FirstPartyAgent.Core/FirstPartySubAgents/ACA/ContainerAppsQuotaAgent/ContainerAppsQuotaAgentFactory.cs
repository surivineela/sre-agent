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
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;
using FirstPartyAgent.Plugins.Definitions;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppsQuotaAgent
{
    // [MENDATORY]
    public class ContainerAppsQuotaAgentFactory 
    {
        private readonly IIcmPlugin icmPlugin;
        private readonly IContainerAppsPlugin containerAppsPlugin;
        private readonly AgentToolsRegistry _toolsRegistry;
        private readonly DurableTaskClient _durableTaskClient;
        private readonly IThreadOrchestrationManager _mappingManager;

        public const string OrchestrationInstanceIdPrefix = nameof(ContainerAppsQuotaAgent);

        public ContainerAppsQuotaAgentFactory(
            ITimePlugin timePlugin,
            IIcmPlugin icmPlugin,
            IContainerAppsPlugin containerAppsPlugin,
            IToolsRepository toolsRepository,
            IThreadOrchestrationManager mappingManager,
            DurableTaskClient durableTaskClient
            )
            
        {
            this.icmPlugin = icmPlugin;
            this.containerAppsPlugin = containerAppsPlugin;
            _toolsRegistry = new AgentToolsRegistry();

            //_toolsRegistry.RegisterTool<MetricsPluginDefinition>(x => x.GetSuccessfulRequestVolumeAsync);
            //_toolsRegistry.RegisterTool<ArmPluginDefinition>(x => x.SetMinimumTlsVersion);
            _toolsRegistry.RegisterPlugin<RecordActionsPluginDefinition>();
            _toolsRegistry.RegisterPlugin<ControlFlowPluginDefinition>();
            _toolsRegistry.RegisterPlugin<ContainerAppsPluginDefinition>();
        }

        public async Task<string> StartOrchestration(
            ContainerAppsQuotaAgentActivityInput input,
            Guid threadId)
        {
            var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";

            await _mappingManager.AddMappingAsync(threadId.ToString(), instanceId);

            await _durableTaskClient.ScheduleNewContainerAppsQuotaAgentInstanceAsync(
                new ContainerAppsQuotaAgentInput(
                    Input: input,
                    ToolSignatures: _toolsRegistry.ToolSignatures,
                    ThreadId: threadId),
                new StartOrchestrationOptions(InstanceId: instanceId));

            return instanceId;
        }

        public ContainerAppsQuotaAgentActivityInput DeserializeInput(string serializedOrchestrationInput)
        {
            return JsonSerializer.Deserialize<ContainerAppsQuotaAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
        }
    }
}
