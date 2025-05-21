// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.Communication;
using Agent.Runtime.SubAgents;
using FirstPartyAgent.Core.Plugins.Definitions;
using FirstPartyAgent.Core.Plugins.Interfaces;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.RevisionAgent
{
    public sealed class ContainerAppRevisionAgentFactory
    {
        private readonly IToolsRepository _toolsRegistry;
        private readonly DurableTaskClient _durableTaskClient;
        private readonly IThreadOrchestrationManager _mappingManager;
        private readonly IReadOnlyList<string> _toolSignatures;
        public const string OrchestrationInstanceIdPrefix = nameof(ContainerAppRevisionAgent);

        public ContainerAppRevisionAgentFactory(
        IMetricsPlugin metricsPlugin,
        ITimePlugin timePlugin,
        IContainerAppRevisionPlugin revisionPlugin,
        IRecordActionsPlugin recordActionsPlugin,
        IManagedEnvironmentPlugin managedEnvironmentPlugin,
        IManagedClusterPlugin managedClusterPlugin,
        IHealthProbePlugin healthProbePlugin,
        INodeAvailabilityPlugin nodeAvailabilityPlugin,
        IChartPlugin chartPlugin,
        IToolsRepository toolsRepository,
        IThreadOrchestrationManager mappingManager,
        DurableTaskClient durableTaskClient)
        {
            _toolsRegistry = toolsRepository;
            var toolSignatures = new List<string>();

            var managedClusterPluginDefinition = new ManagedClusterPluginDefinition(managedClusterPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => managedClusterPluginDefinition.GetASIPageForManagedCluster));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => managedClusterPluginDefinition.GetAksClusterCcpNamespace));

            var managedEnvironmentPluginDefinition = new ManagedEnvironmentPluginDefinition(managedEnvironmentPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => managedEnvironmentPluginDefinition.GetManagedEnvironmentInfo));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => managedEnvironmentPluginDefinition.GetASIPageForManagedEnvironment));

            var healthProbePluginDefinition = new HealthProbePluginDefinition(healthProbePlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => healthProbePluginDefinition.GetHealthProbeFailures));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => healthProbePluginDefinition.GetHealthProbeSettings));

            var nodeAvailabilityPluginDefinition = new NodeAvailabilityPluginDefinition(nodeAvailabilityPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => nodeAvailabilityPluginDefinition.GetNodeAvailabilityFailures));

            var remediationPluginDefinition = new ContainerAppRevisionPluginDefinition(revisionPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => remediationPluginDefinition.ListRevisions));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => remediationPluginDefinition.GetRevisionTrafficWithReplicaCount));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => remediationPluginDefinition.GetEventProcessorEventsWithoutReplica));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => remediationPluginDefinition.ContainerAppRevisionStatus));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => remediationPluginDefinition.GetActiveRevisionSessions));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => remediationPluginDefinition.GetASIPageForRevision));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => remediationPluginDefinition.GetHpaHeartbeatMetrics));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => remediationPluginDefinition.GetHttpScalerEventsForContainerApp));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => remediationPluginDefinition.GetInternalEventProcessorEventsForPod));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => remediationPluginDefinition.GetRevisionSpecChanges));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => remediationPluginDefinition.GetLegionErrors));

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
            ContainerAppRevisionAgentActivityInput input,
            Guid threadId)
        {
            var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";

            await _mappingManager.AddMappingAsync(threadId.ToString(), instanceId);

            await _durableTaskClient.ScheduleNewContainerAppRevisionAgentInstanceAsync(
                new RevisionAgentInput(
                    Input: input,
                    ToolSignatures: _toolSignatures,
                    ThreadId: threadId),
                new StartOrchestrationOptions(InstanceId: instanceId));

            return instanceId;
        }

        public ContainerAppRevisionAgentActivityInput DeserializeInput(string serializedOrchestrationInput)
        {
            return JsonSerializer.Deserialize<RevisionAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
        }
    }
  
}
