// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core;
using Agent.Core.Models.Api.v1;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.Communication;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using System.Text.Json;

namespace Agent.Runtime.SubAgents.StorageAccountAgent
{
    public class StorageAccountAgentFactory
    {
        private readonly IReadOnlyList<string> _toolSignatures;
        private readonly DurableTaskClient _durableTaskClient;
        private readonly IThreadOrchestrationManager _mappingManager;

        public const string OrchestrationInstanceIdPrefix = nameof(StorageAccountAgent);

        public StorageAccountAgentFactory(
            IApprovalPlugin approvalPlugin,
            ITimePlugin timePlugin,
            IRemediationPlugin remediationPlugin,
            IRecordActionsPlugin recordActionsPlugin,
            ToolsRepository toolsRepository,
            IThreadOrchestrationManager mappingManager,
            DurableTaskClient durableTaskClient
            )
        {
            var toolSignatures = new List<string>();

            var remediationPluginDefinition = new RemediationPluginDefinition(remediationPlugin);
            toolSignatures.Add(toolsRepository.GetSignature(() => remediationPluginDefinition.StorageAccountDisableSharedKeySupport));
            toolSignatures.Add(toolsRepository.GetSignature(() => remediationPluginDefinition.StorageAccountDisablePublicContainers));

            var recordActionsPluginDefinition = new RecordActionsPluginDefinition(recordActionsPlugin);
            toolSignatures.Add(toolsRepository.GetSignature(() => recordActionsPluginDefinition.RecordAction));
            toolSignatures.Add(toolsRepository.GetSignature(() => recordActionsPluginDefinition.GetActionDetails));

            var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
            toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
            toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
            toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));

            var approvalPluginDefinition = new ApprovalPluginDefinition(approvalPlugin);
            toolSignatures.Add(toolsRepository.GetSignature(() => approvalPluginDefinition.StartApprovalFlow));

            _toolSignatures = toolSignatures;
            _durableTaskClient = durableTaskClient;
            _mappingManager = mappingManager;
        }

        public async Task<string> StartOrchestration(StorageAccountAgentPlanInput input, ThreadContext context)
        {
            var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";
            var threadId = context.ThreadId.ToString();

            await _mappingManager.AddMappingAsync(threadId, instanceId);

            return await _durableTaskClient.ScheduleNewStorageAccountAgentInstanceAsync(
                new StorageAccountAgentInput(Input: input, ToolSignatures: _toolSignatures, context),
                new StartOrchestrationOptions(InstanceId: instanceId)
            );
        }

        public StorageAccountAgentPlanInput DeserializeInput(string serializedOrchestrationInput)
        {
            return JsonSerializer.Deserialize<StorageAccountAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
        }
    }
}

