using System.Text.Json;
using Agent.Core;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Agent.Runtime.Communication;
using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.SubAgents.DailyReportSummary;

public sealed class DailyReportSummaryAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IThreadOrchestrationManager _mappingManager;
    public const string OrchestrationInstanceIdPrefix = nameof(DailyReportSummaryAgent);

    public DailyReportSummaryAgentFactory(
        IMetricsPlugin metricsPlugin,
        IGrafanaPlugin grafanaPlugin,
        IApprovalPlugin approvalPlugin,
        IRecordActionsPlugin recordActionsPlugin,
        IGraphDBPlugin graphDBPlugin,
        ToolsRepository toolsRepository,
        IThreadOrchestrationManager mappingManager,
        DurableTaskClient durableTaskClient)
    {
        var toolSignatures = new List<string>();

        // Metrics plugin for basic telemetry
        var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => metricsPluginDefinition.GetSuccessfulRequestVolumeAsync));

        // Grafana plugin for dashboard and visualization
        var grafanaPluginDefinition = new GrafanaPluginDefinition(grafanaPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => grafanaPluginDefinition.ModifyGrafanaDashboard));

        var graphDBPluginDefinition = new GraphDBPluginDefinition(graphDBPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => graphDBPluginDefinition.VisualizeApplicationComponents));
        toolSignatures.Add(toolsRepository.GetSignature(() => graphDBPluginDefinition.DiscoverApplications));
        toolSignatures.Add(toolsRepository.GetSignature(() => graphDBPluginDefinition.GetApplicationComponentsSummary));
        toolSignatures.Add(toolsRepository.GetSignature(() => graphDBPluginDefinition.FindAllNetworkConnectedResources));

        // Control flow plugin for basic orchestration functions
        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        // Approval plugin for user interactions
        var approvalPluginDefinition = new ApprovalPluginDefinition(approvalPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => approvalPluginDefinition.StartApprovalFlow));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
        _mappingManager = mappingManager;
    }

    public async Task<string> StartOrchestration(
        DailyReportSummaryInput input,
        ThreadContext context)
    {
        var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";

        var threadId = context.ThreadId.ToString();

        await _mappingManager.AddMappingAsync(threadId, instanceId);

        return await _durableTaskClient.ScheduleNewDailyReportSummaryAgentInstanceAsync(
            new DailyReportSummaryAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                Context: context),
            new StartOrchestrationOptions(InstanceId: $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}"));
    }

    public DailyReportSummaryInput DeserializeInput(string serializedOrchestraionInput)
    {
        return JsonSerializer.Deserialize<DailyReportSummaryAgentInput>(serializedOrchestraionInput).ThrowIfNull().Input;
    }
}
