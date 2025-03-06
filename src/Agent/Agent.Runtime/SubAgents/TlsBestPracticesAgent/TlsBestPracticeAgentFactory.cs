using Agent.Plugins.Definitions;
using Agent.Plugins;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask;
using Agent.Core.Models;

namespace Agent.Runtime.SubAgents.TlsBestPractices;

// [Export]
public sealed class TlsBestPracticeAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;

    public TlsBestPracticeAgentFactory(
        IMetricsPlugin metricsPlugin,
        IArmPlugin armPlugin,
        IApprovalPlugin approvalPlugin,
        ToolsRepository toolsRepository,
        DurableTaskClient durableTaskClient)
    {
        var toolSignatures = new List<string>();
        var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => metricsPluginDefinition.GetSuccessfulRequestVolumeAsync));

        var armPluginDefinition = new ArmPluginDefinition(armPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => armPluginDefinition.SetMinimumTlsVersion));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));

        var approvalPluginDefinition = new ApprovalPluginDefinition(approvalPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => approvalPluginDefinition.StartApprovalFlow));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
    }

    public async Task<string> StartOrchestration(
        TlsBestPracticesInput input)
    {
        return await _durableTaskClient.ScheduleNewTlsBestPracticesAgentInstanceAsync(
            new TlsBestPracticesAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures));
    }
}
