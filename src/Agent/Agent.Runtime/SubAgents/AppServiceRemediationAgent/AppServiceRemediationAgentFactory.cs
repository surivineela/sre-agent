using Agent.Plugins.Definitions;
using Agent.Plugins;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask;
using Agent.Core.Plugins;
using Agent.Core.Models;
using Agent.Runtime.SubAgents.ManagedIdentityMigration;
using System.Text.Json;
using Agent.Core;

namespace Agent.Runtime.SubAgents.AppServiceRemediation;

// [Export]
public sealed class AppServiceRemediationAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;

    public const string OrchestrationInstanceIdPrefix = nameof(AppServiceRemediationAgent);

    public AppServiceRemediationAgentFactory(
        IMetricsPlugin metricsPlugin,
        IArmPlugin armPlugin,
        IApprovalPlugin approvalPlugin,
        ITimePlugin timePlugin,
        IRemediationPlugin remediationPlugin,
        ToolsRepository toolsRepository,
        DurableTaskClient durableTaskClient)
    {
        var toolSignatures = new List<string>();
        var timePluginDefinition = new TimePluginDefinition(timePlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => timePluginDefinition.GetCurrentUtcTime));
        toolSignatures.Add(toolsRepository.GetSignature(() => timePluginDefinition.GetAppTimeZone));

        var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => metricsPluginDefinition.StartGetWebAppCpuMetrics));
        toolSignatures.Add(toolsRepository.GetSignature(() => metricsPluginDefinition.StartGetMemoryMetrics));
        toolSignatures.Add(toolsRepository.GetSignature(() => metricsPluginDefinition.GetFunctionAppRequestAvailability));

        var remediationPluginDefinition = new RemediationPluginDefinition(remediationPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => remediationPluginDefinition.ScaleAppServicePlanVertically));
        toolSignatures.Add(toolsRepository.GetSignature(() => remediationPluginDefinition.SuggestNextSku));
        toolSignatures.Add(toolsRepository.GetSignature(() => remediationPluginDefinition.CalculateScalingCost));
        toolSignatures.Add(toolsRepository.GetSignature(() => remediationPluginDefinition.RestartWebApp));
        toolSignatures.Add(toolsRepository.GetSignature(() => remediationPluginDefinition.CollectMemoryDump));

        var armPluginDefinition = new ArmPluginDefinition(armPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => armPluginDefinition.RestartWebApp));

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
        string input,
        string threadId = "")
    {
        return await _durableTaskClient.ScheduleNewAppServiceRemediationAgentInstanceAsync(
            new AppServiceRemediationAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                threadId),
            new StartOrchestrationOptions(InstanceId: $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}"));
    }

    public string DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<AppServiceRemediationAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
    }
}
