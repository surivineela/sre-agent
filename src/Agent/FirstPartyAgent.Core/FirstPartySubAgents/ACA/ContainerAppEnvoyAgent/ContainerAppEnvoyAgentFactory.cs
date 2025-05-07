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
        private readonly IToolsRepository _toolsRegistry;
        private readonly DurableTaskClient _durableTaskClient;
        private readonly IThreadOrchestrationManager _mappingManager;
        private readonly IReadOnlyList<string> _toolSignatures;
        public const string OrchestrationInstanceIdPrefix = nameof(ContainerAppEnvoyAgentInput);

        public ContainerAppEnvoyAgentFactory(
            IContainerAppEnvoyPlugin envoyPlugin,
            IToolsRepository toolsRepository,
            IThreadOrchestrationManager mappingManager,
            DurableTaskClient durableTaskClient)
        {
            _toolsRegistry = toolsRepository;
            var toolSignatures = new List<string>();

            var envoyPluginDefinition = new ContainerAppEnvoyPluginDefinition(envoyPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => envoyPluginDefinition.GetContainerAppManagedCluster));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => envoyPluginDefinition.GetEnvoyAbnormalLogs));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => envoyPluginDefinition.GetEnvoyControllerLogs));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => envoyPluginDefinition.GetEnvoyAccessLogs));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => envoyPluginDefinition.GetSwiftNetworkingEvents));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => envoyPluginDefinition.GetContainerAppAdminEvents));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => envoyPluginDefinition.GetContainerAppStatus));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => envoyPluginDefinition.GetEnvoyPodStatus));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => envoyPluginDefinition.GetEnvoyControllerPodStatus));

            var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
            toolSignatures.Add(_toolsRegistry.GetSignature(() => controlFlowPluginDefinition.Wait));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

            _toolSignatures = toolSignatures;
            _durableTaskClient = durableTaskClient;
            _mappingManager = mappingManager;
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
                    ToolSignatures: _toolSignatures,
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
