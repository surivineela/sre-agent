using Agent.Plugins.Definitions;
using Agent.Plugins;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask;
using System.Text.Json;
using Agent.Core;
using Agent.Core.Plugins;

namespace Agent.Runtime.SubAgents.ContainerAppsRemediation;

// [Export]
public sealed class ContainerAppsRemediationAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;

    public const string OrchestrationInstanceIdPrefix = nameof(ContainerAppsRemediationAgent);

    public ContainerAppsRemediationAgentFactory(
        IMetricsPlugin metricsPlugin,
        IArmPlugin armPlugin,
        IApprovalPlugin approvalPlugin,
        ITimePlugin timePlugin,
        IRemediationPlugin remediationPlugin,
        IRecordActionsPlugin recordActionsPlugin,
        IChartPlugin chartPlugin,
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

        var chartPluginDefinition = new ChartPluginDefinition(chartPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => chartPluginDefinition.PlotTimeSeriesDataAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => chartPluginDefinition.PlotPieChartAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => chartPluginDefinition.PlotBarChartAsync));

        var recordActionsPluginDefinition = new RecordActionsPluginDefinition(recordActionsPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => recordActionsPluginDefinition.RecordAction));
        toolSignatures.Add(toolsRepository.GetSignature(() => recordActionsPluginDefinition.GetActionDetails));

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
        return await _durableTaskClient.ScheduleNewContainerAppsRemediationAgentInstanceAsync(
            new ContainerAppsRemediationAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                threadId),
            new StartOrchestrationOptions(InstanceId: $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}"));
    }

    public string DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<ContainerAppsRemediationAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
    }
}
