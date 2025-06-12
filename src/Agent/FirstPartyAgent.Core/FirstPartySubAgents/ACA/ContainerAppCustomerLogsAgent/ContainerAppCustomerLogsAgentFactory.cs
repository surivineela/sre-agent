// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core;
using Agent.Plugins;
using Agent.Plugins.Interface;
using Agent.Runtime.Communication;
using Agent.Runtime.SubAgents;
using FirstPartyAgent.Core.Plugins.Definitions;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;
using FirstPartyAgent.Plugins.Definitions;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using IContainerAppIcMPlugin = FirstPartyAgent.Core.Plugins.Interfaces.IContainerAppIcMPlugin;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppCustomerLogsAgent
{
    public class ContainerAppCustomerLogsAgentFactory
    {
        private readonly IToolsRepository _toolsRegistry;
        private readonly DurableTaskClient _durableTaskClient;
        private readonly IThreadOrchestrationManager _mappingManager;
        private readonly IReadOnlyList<string> _toolSignatures;
        public const string OrchestrationInstanceIdPrefix = nameof(ContainerAppCustomerLogsAgent);

        public ContainerAppCustomerLogsAgentFactory(
            IMetricsPlugin metricsPlugin,
            ITimePlugin timePlugin,
            IContainerAppIcMPlugin containerAppIcMPlugin,
            IManagedClusterPlugin managedClusterPlugin,
            IManagedEnvironmentPlugin managedEnvironmentPlugin,
            IToolsRepository toolsRepository,
            IThreadOrchestrationManager mappingManager,
            DurableTaskClient durableTaskClient,
            IContainerAppCustomerLogsPlugin containerAppCustomerLogsPlugin,
            IContainerAppRevisionPlugin revisionPlugin,
            IContainerAppEnvoyPlugin envoyPlugin)
        {
            _toolsRegistry = toolsRepository;
            var toolSignatures = new List<string>();

            //Add more plugins as required

            var logsMetricsPluginDefinition = new ContainerAppCustomerLogsPluginDefinition(containerAppCustomerLogsPlugin, managedClusterPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => logsMetricsPluginDefinition.GetLogConfiguration));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => logsMetricsPluginDefinition.GetEventProcessorLeaderElectionEvents));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => logsMetricsPluginDefinition.GetEventProcessorErrors));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => logsMetricsPluginDefinition.GetAppsAndjobsVolumeForEnv));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => logsMetricsPluginDefinition.GetEventProcessorPods));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => logsMetricsPluginDefinition.GetLogProcessorPods));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => logsMetricsPluginDefinition.GetEventProcessorPodStatus));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => logsMetricsPluginDefinition.GetLogProcessorPodStatus));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => logsMetricsPluginDefinition.GetContainerAppWorkloadProfile));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => logsMetricsPluginDefinition.GetInputPressureOnLogProcessor));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => logsMetricsPluginDefinition.GetMemoryPressureOnFluentbit));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => logsMetricsPluginDefinition.GetFluentbitOutputCount));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => logsMetricsPluginDefinition.GetFluentbitBufferPressure));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => logsMetricsPluginDefinition.GetFluentbitOutputErrors));


            var envoyPluginDefinition = new ContainerAppEnvoyPluginDefinition(envoyPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => envoyPluginDefinition.GetContainerAppManagedCluster));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => envoyPluginDefinition.GetContainerAppStatus));

            var managedEnvironmentPluginDefinition = new ManagedEnvironmentPluginDefinition(managedEnvironmentPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => managedEnvironmentPluginDefinition.GetManagedEnvironmentInfo));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => managedEnvironmentPluginDefinition.GetASIPageForManagedEnvironment));

            var managedClusterPluginDefinition = new ManagedClusterPluginDefinition(managedClusterPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => managedClusterPluginDefinition.GetASIPageForManagedCluster));

             var containerAppRevisionPluginDefinition = new ContainerAppRevisionPluginDefinition(revisionPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppRevisionPluginDefinition.GetPodHeartbeatStatus));

            var containerAppIcMPluginDefinition = new ContainerAppIcMPluginDefinition(containerAppIcMPlugin);
            // READ operations
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppIcMPluginDefinition.GetInitialInvestigationReportAsync));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => containerAppIcMPluginDefinition.GetIssueInvestigationTimeRange));
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
           ContainerAppCustomerLogsAgentActivityInput input,
           Guid threadId)
        {
            var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";

            await _mappingManager.AddMappingAsync(threadId.ToString(), instanceId);

            await _durableTaskClient.ScheduleNewContainerAppCustomerLogsAgentInstanceAsync(
                new CustomerLogsAgentInput(
                    Input: input,
                    ToolSignatures: _toolSignatures,
                    ThreadId: threadId),
                new StartOrchestrationOptions(InstanceId: instanceId));

            return instanceId;
        }

        public ContainerAppCustomerLogsAgentActivityInput DeserializeInput(string serializedOrchestrationInput)
        {
            return JsonSerializer.Deserialize<CustomerLogsAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
        }
    }
}
