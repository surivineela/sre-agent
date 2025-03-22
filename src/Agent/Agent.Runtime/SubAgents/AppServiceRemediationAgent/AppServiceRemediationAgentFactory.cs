using Agent.Plugins.Definitions;
using Agent.Plugins;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask;
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
        // TODO: use StartGetXXX once we have DTS version of the plugin
        toolSignatures.Add(toolsRepository.GetSignature(() => metricsPluginDefinition.GetWebAppCpuMetrics));
        toolSignatures.Add(toolsRepository.GetSignature(() => metricsPluginDefinition.GetMemoryMetrics));
        toolSignatures.Add(toolsRepository.GetSignature(() => metricsPluginDefinition.GetFunctionAppRequestAvailability));
        toolSignatures.Add(toolsRepository.GetSignature(() => metricsPluginDefinition.GetSuccessfulRequestVolumeAsync));

        var chartPluginDefinition = new ChartPluginDefinition(chartPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => chartPluginDefinition.PlotTimeSeriesDataAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => chartPluginDefinition.PlotPieChartAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => chartPluginDefinition.PlotBarChartAsync));

        var remediationPluginDefinition = new RemediationPluginDefinition(remediationPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => remediationPluginDefinition.ScaleAppServicePlanVertically));
        toolSignatures.Add(toolsRepository.GetSignature(() => remediationPluginDefinition.SuggestNextSku));
        toolSignatures.Add(toolsRepository.GetSignature(() => remediationPluginDefinition.CalculateScalingCost));
        toolSignatures.Add(toolsRepository.GetSignature(() => remediationPluginDefinition.RestartWebApp));
        toolSignatures.Add(toolsRepository.GetSignature(() => remediationPluginDefinition.CollectMemoryDump));

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
    }

    public async Task<string> StartOrchestration(
        AppServiceRemediationInput input,
        string threadId)
    {
        return await _durableTaskClient.ScheduleNewAppServiceRemediationAgentInstanceAsync(
            new AppServiceRemediationAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                threadId),
            new StartOrchestrationOptions(InstanceId: $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}"));
    }

    public AppServiceRemediationInput DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<AppServiceRemediationAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
    }
}
