// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------


using System.Text.Json;
using Agent.Core;
using Agent.Plugins;
using Agent.Runtime;
using Agent.Runtime.Communication;
using Agent.Runtime.SubAgents;
using FirstPartyAgent.Core.Plugins.Definitions;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;
using FirstPartyAgent.Plugins.Definitions;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppEnvironmentAgent
{
    public class ContainerAppEnvironmentAgentFactory
    {
        private readonly IToolsRepository _toolsRegistry;
        private readonly DurableTaskClient _durableTaskClient;
        private readonly IThreadOrchestrationManager _mappingManager;
        private readonly IReadOnlyList<string> _toolSignatures;
        public const string OrchestrationInstanceIdPrefix = nameof(ContainerAppEnvironmentAgent);

        public ContainerAppEnvironmentAgentFactory(
            IContainerAppsPlugin containerAppsPlugin,
            IManagedEnvironmentPlugin managedEnvironmentPlugin,
            IToolsRepository toolsRepository,
            IThreadOrchestrationManager mappingManager,
            DurableTaskClient durableTaskClient
            )

        {
            _toolsRegistry = toolsRepository;
            var toolSignatures = new List<string>();

            var managedEnvironmentPluginDefinition = new ManagedEnvironmentPluginDefinition(managedEnvironmentPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => managedEnvironmentPluginDefinition.GetManagedEnvironmentInfo));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => managedEnvironmentPluginDefinition.GetManagedClusterEnvironmentResourceId));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => managedEnvironmentPluginDefinition.GetManagedEnvironmentProvisioningStatus));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => managedEnvironmentPluginDefinition.GetManagedEnvironmentAdminEvents));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => managedEnvironmentPluginDefinition.GetManagedEnvironmentOperationErrors));

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
            ContainerAppEnvironmentAgentActivityInput input,
            Guid threadId)
        {
            var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";

            await _mappingManager.AddMappingAsync(threadId.ToString(), instanceId);

            await _durableTaskClient.ScheduleNewContainerAppEnvironmentAgentInstanceAsync(
                new ContainerAppEnvironmentAgentInput(
                    Input: input,
                    ToolSignatures: _toolSignatures,
                    ThreadId: threadId),
                new StartOrchestrationOptions(InstanceId: instanceId));

            return instanceId;
        }

        public ContainerAppEnvironmentAgentActivityInput DeserializeInput(string serializedOrchestrationInput)
        {
            return JsonSerializer.Deserialize<ContainerAppEnvironmentAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
        }
    }
}
