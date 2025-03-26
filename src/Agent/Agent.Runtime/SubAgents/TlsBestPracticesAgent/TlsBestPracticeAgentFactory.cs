using Agent.Plugins.Definitions;
using Agent.Plugins;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask;
using Agent.Core.Models;
using Agent.Runtime.SubAgents.ManagedIdentityMigration;
using System.Text.Json;
using Agent.Core;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.Communication;

namespace Agent.Runtime.SubAgents.TlsBestPractices;

// [Export]
public sealed class TlsBestPracticeAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IThreadOrchestrationManager _mappingManager;

    public const string OrchestrationInstanceIdPrefix = nameof(TlsBestPracticesAgent);

    public TlsBestPracticeAgentFactory(
        IMetricsPlugin metricsPlugin,
        IArmPlugin armPlugin,
        IApprovalPlugin approvalPlugin,
        IRecordActionsPlugin recordActionsPlugin,
        ToolsRepository toolsRepository,
        IThreadOrchestrationManager mappingManager,
        DurableTaskClient durableTaskClient)
    {
        var toolSignatures = new List<string>();
        var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => metricsPluginDefinition.GetSuccessfulRequestVolumeAsync));

        var armPluginDefinition = new ArmPluginDefinition(armPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => armPluginDefinition.SetMinimumTlsVersion));
        // toolSignatures.Add(toolsRepository.GetSignature(() => armPluginDefinition.GetTlsSettings));

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

    public async Task<string> StartOrchestration(
        TlsBestPracticesInput input,
        string threadId = "")
    {
        var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";

        if (threadId != null)
        {
            await _mappingManager.AddMappingAsync(new ThreadOrchestrationMapping(
                Id: $"mapping_{threadId}",
                ThreadId: threadId,
                OrchestrationInstanceId: instanceId,
                CreatedTimestamp: DateTime.UtcNow,
                ModifiedTimestamp: DateTime.UtcNow
                )
            );
        }

        await _durableTaskClient.ScheduleNewTlsBestPracticesAgentInstanceAsync(
            new TlsBestPracticesAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                ThreadId: threadId),
            new StartOrchestrationOptions(InstanceId: instanceId));

        return instanceId;
    }

    public TlsBestPracticesInput DeserializeInput(string serializedOrchestraionInput)
    {
        return JsonSerializer.Deserialize<TlsBestPracticesAgentInput>(serializedOrchestraionInput).ThrowIfNull().Input;
    }
}
