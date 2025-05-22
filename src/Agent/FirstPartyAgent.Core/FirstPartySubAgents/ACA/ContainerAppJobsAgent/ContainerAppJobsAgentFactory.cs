// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.Communication;
using FirstPartyAgent.Core.Plugins.Definitions;
using Microsoft.DurableTask.Client;
using Agent.Runtime.SubAgents;
using Microsoft.DurableTask;
using FirstPartyAgent.Plugins.Interfaces;
using FirstPartyAgent.Core.Plugins.Interfaces;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppJobsAgent
{
    public sealed class ContainerAppJobsAgentFactory
    {
        private readonly IToolsRepository _toolsRegistry;
        private readonly DurableTaskClient _durableTaskClient;
        private readonly IThreadOrchestrationManager _mappingManager;
        private readonly IReadOnlyList<string> _toolSignatures;
        public const string OrchestrationInstanceIdPrefix = nameof(ContainerAppJobsAgent);

        public ContainerAppJobsAgentFactory(
            IContainerAppJobsPlugin jobsAgentPlugin,
            IToolsRepository toolsRepository,
            IThreadOrchestrationManager mappingManager,
            IManagedClusterPlugin managedClusterPlugin,
            DurableTaskClient durableTaskClient,
            IRecordActionsPlugin recordActionsPlugin,
            ITimePlugin timePlugin)
        {
            _toolsRegistry = toolsRepository;
            var toolSignatures = new List<string>();

            // Instantiate JobsAgentPluginDefinition using the provided IJobsAgentPlugin
            var jobsAgentPluginDefinition = new ContainerAppJobsPluginDefinition(jobsAgentPlugin);

            // Register KernelFunctions from JobsAgentPluginDefinition
            toolSignatures.Add(_toolsRegistry.GetSignature(() => jobsAgentPluginDefinition.GetJobDefinition));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => jobsAgentPluginDefinition.GetJobExecutionFinalStatus));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => jobsAgentPluginDefinition.GetJobExecutionEvents));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => jobsAgentPluginDefinition.GetAllJobExecutionsErrorEvents));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => jobsAgentPluginDefinition.GetAllJobExecutionsFinalStatus));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => jobsAgentPluginDefinition.GetKedaEventsForJobScaledJobs));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => jobsAgentPluginDefinition.GetLegionVKEventsForJobsRunningConsumptionV2));

            var managedClusterPluginDefinition = new ManagedClusterPluginDefinition(managedClusterPlugin);
            toolSignatures.Add(_toolsRegistry.GetSignature(() => managedClusterPluginDefinition.GetSystemComponentErrorEvents));

            // Register ControlFlow tools - assuming parameterless constructor for now
            // If ControlFlowPluginDefinition is enhanced to take these, the call can be updated.
            var controlFlowPluginDefinition = new ControlFlowPluginDefinition(); 
            // TODO: Verify if recordActionsPlugin and timePlugin should be passed to ControlFlowPluginDefinition
            // If so, the ControlFlowPluginDefinition constructor and its usage here would need to change.
            // For now, matching ContainerAppRevisionAgentFactory's parameterless instantiation.

            toolSignatures.Add(_toolsRegistry.GetSignature(() => controlFlowPluginDefinition.Wait));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
            toolSignatures.Add(_toolsRegistry.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

            _toolSignatures = toolSignatures;
            _durableTaskClient = durableTaskClient;
            _mappingManager = mappingManager;
        }

        public async Task<string> StartOrchestration(
            ContainerAppJobsAgentActivityInput input,
            Guid threadId)
        {
            var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";

            await _mappingManager.AddMappingAsync(threadId.ToString(), instanceId);

            await _durableTaskClient.ScheduleNewOrchestrationInstanceAsync(
                nameof(ContainerAppJobsAgent), 
                new ContainerAppJobsAgentInput(
                    Input: input,
                    ToolSignatures: _toolSignatures,
                    ThreadId: threadId),
                new StartOrchestrationOptions(InstanceId: instanceId));

            return instanceId;
        }

        public ContainerAppJobsAgentActivityInput DeserializeInput(string serializedOrchestrationInput)
        {
            var jobsAgentInput = JsonSerializer.Deserialize<ContainerAppJobsAgentInput>(serializedOrchestrationInput);
            if (jobsAgentInput == null)
            {
                throw new JsonException($"Failed to deserialize {nameof(ContainerAppJobsAgentInput)} from the provided string.");
            }
            return jobsAgentInput.Input;
        }
    }
}
