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
using FirstPartyAgent.Plugins.Definitions;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppSessionsAgent
{
    public sealed class ContainerAppSessionsAgentFactory
    {
        private readonly IToolsRepository _toolsRegistry;
        private readonly DurableTaskClient _durableTaskClient;
        private readonly IThreadOrchestrationManager _mappingManager;
        private readonly IReadOnlyList<string> _toolSignatures;
        public const string OrchestrationInstanceIdPrefix = nameof(ContainerAppSessionsAgent);

        public ContainerAppSessionsAgentFactory(
        IMetricsPlugin metricsPlugin,
        ITimePlugin timePlugin,
        IContainerAppSessionsPlugin sessionsPlugin,
        IContainerAppIcMPlugin containerAppIcMPlugin,
        IManagedClusterPlugin  managedClusterPlugin,
        IChartPlugin chartPlugin,
        IToolsRepository toolsRepository,
        IThreadOrchestrationManager mappingManager,
        DurableTaskClient durableTaskClient)
        {
            _toolsRegistry = toolsRepository;
            var toolSignatures = new List<string>();

            var sessionPluginDefinition = new ContainerAppSessionsPluginDefinition(sessionsPlugin);
            // READ operations
            toolSignatures.Add(_toolsRegistry.GetSignature(() => sessionPluginDefinition.GetSessionPoolInfo));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => sessionPluginDefinition.GetChangesInSessionPool));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => sessionPluginDefinition.GetSessionPodLogs));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => sessionPluginDefinition.GetSessionPoolCreateOrUpdateLogs));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => sessionPluginDefinition.GetCodeInterpreterSessionExecutionEventLogs));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => sessionPluginDefinition.GetCustomContainerSessionActivatorLogs));

            var managedClusterPluginDefinition = new ManagedClusterPluginDefinition(managedClusterPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => managedClusterPluginDefinition.GetASIPageForManagedCluster));

            var containerAppIcMPluginDefinition = new ContainerAppIcMPluginDefinition(containerAppIcMPlugin);
            // READ operations
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppIcMPluginDefinition.GetInitialInvestigationReportAsync));
            // WRITE operations
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppIcMPluginDefinition.AddDiscussionEntry));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppIcMPluginDefinition.WasAgentHelpfulInDebuggingIssueAsync));


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
            ContainerAppSessionsAgentActivityInput input,
            Guid threadId)
        {
            var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";

            await _mappingManager.AddMappingAsync(threadId.ToString(), instanceId);

            await _durableTaskClient.ScheduleNewContainerAppSessionsAgentInstanceAsync(
                new SessionsAgentInput(
                    Input: input,
                    ToolSignatures: _toolSignatures,
                    ThreadId: threadId),
                new StartOrchestrationOptions(InstanceId: instanceId));

            return instanceId;
        }

        public ContainerAppSessionsAgentActivityInput DeserializeInput(string serializedOrchestrationInput)
        {
            return JsonSerializer.Deserialize<SessionsAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
        }
    }
}
