// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core;
using Agent.Plugins;
using Agent.Runtime.Communication;
using Agent.Runtime.SubAgents;
using FirstPartyAgent.Core.Plugins.Definitions;
using FirstPartyAgent.Core.Plugins.Implementation;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;
using FirstPartyAgent.Plugins.Definitions;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppIcmAgent
{
    public sealed class ContainerAppIcmAgentFactory
    {
        private readonly IToolsRepository _toolsRegistry;
        private readonly DurableTaskClient _durableTaskClient;
        private readonly IThreadOrchestrationManager _mappingManager;
        private readonly IReadOnlyList<string> _toolSignatures;
        public const string OrchestrationInstanceIdPrefix = nameof(ContainerAppIcmAgent);

        public ContainerAppIcmAgentFactory(
        IMetricsPlugin metricsPlugin,
        ITimePlugin timePlugin,
        IContainerAppIcMPlugin containerAppIcMPlugin,
        IContainerAppsPlugin containerAppsPlugin,
        IChartPlugin chartPlugin,
        IManagedClusterPlugin managedClusterPlugin,
        IManagedEnvironmentPlugin managedEnvironmentPlugin,
        IToolsRepository toolsRepository,
        IThreadOrchestrationManager mappingManager,
        DurableTaskClient durableTaskClient)
        {
            _toolsRegistry = toolsRepository;
            var toolSignatures = new List<string>();

            var timePluginDefinition = new TimePluginDefinition(timePlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => timePluginDefinition.GetCurrentUtcTime));

            var containerAppsPluginDefinition = new ContainerAppsPluginDefinition(containerAppsPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppsPluginDefinition.GetSubscriptionDetail));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppsPluginDefinition.GetSubscriptionUsage));

            var managedClusterPluginDefinition = new ManagedClusterPluginDefinition(managedClusterPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => managedClusterPluginDefinition.GetManagedClusterInformation));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => managedClusterPluginDefinition.GetASIPageForManagedCluster));

            var managedEnvironmentPluginDefinition = new ManagedEnvironmentPluginDefinition(managedEnvironmentPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => managedEnvironmentPluginDefinition.GetManagedEnvironmentInfo));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => managedEnvironmentPluginDefinition.GetASIPageForManagedEnvironment));

            var containerAppIcMPluginDefinition = new ContainerAppIcMPluginDefinition(containerAppIcMPlugin);
            // READ operations
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppIcMPluginDefinition.GetIssueInvestigationTimeRange));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppIcMPluginDefinition.GetInitialInvestigationReportAsync));

            // keep it disabled to minimize the model context
            // Instead of these two mthods, we have a single method `GetInitialInvestigationReportAsync` which fetches both incident and discussion entries
            //toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppIcMPluginDefinition.GetIncidentInfo));
            //toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppIcMPluginDefinition.GetDiscussionEntries));

            // WRITE operations
            //toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppIcMPluginDefinition.MitigateIncident));
            //toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppIcMPluginDefinition.ResolveIncident));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppIcMPluginDefinition.AddDiscussionEntry));

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
            ContainerAppIcmAgentActivityInput input,
            Guid threadId)
        {
            var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";

            await _mappingManager.AddMappingAsync(threadId.ToString(), instanceId);

            await _durableTaskClient.ScheduleNewContainerAppIcmAgentInstanceAsync(
                new ContainerAppIcmAgentInput(
                    Input: input,
                    ToolSignatures: _toolSignatures,
                    ThreadId: threadId),
                new StartOrchestrationOptions(InstanceId: instanceId));

            return instanceId;
        }

        public ContainerAppIcmAgentActivityInput DeserializeInput(string serializedOrchestrationInput)
        {
            return JsonSerializer.Deserialize<ContainerAppIcmAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
        }
    }
}
