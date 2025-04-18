// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

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
        toolSignatures.Add(ToolsRepository.GetSignature(() => metricsPluginDefinition.GetSuccessfulRequestVolumeAsync));

        // Grafana plugin for dashboard and visualization
        var grafanaPluginDefinition = new GrafanaPluginDefinition(grafanaPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => grafanaPluginDefinition.ModifyGrafanaDashboard));

        var graphDBPluginDefinition = new GraphDBPluginDefinition(graphDBPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => graphDBPluginDefinition.VisualizeApplicationComponents));
        toolSignatures.Add(ToolsRepository.GetSignature(() => graphDBPluginDefinition.DiscoverApplications));
        toolSignatures.Add(ToolsRepository.GetSignature(() => graphDBPluginDefinition.GetApplicationComponentsSummary));
        toolSignatures.Add(ToolsRepository.GetSignature(() => graphDBPluginDefinition.FindAllNetworkConnectedResources));

        // Control flow plugin for basic orchestration functions
        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        // Approval plugin for user interactions
        var approvalPluginDefinition = new ApprovalPluginDefinition(approvalPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => approvalPluginDefinition.StartApprovalFlow));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
        _mappingManager = mappingManager;
    }

    public async Task<string> StartOrchestration(
        DailyReportSummaryInput input,
        Guid threadId)
    {
        var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";

        await _mappingManager.AddMappingAsync(threadId.ToString(), instanceId);

        return await _durableTaskClient.ScheduleNewDailyReportSummaryAgentInstanceAsync(
            new DailyReportSummaryAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                ThreadId: threadId),
            new StartOrchestrationOptions(InstanceId: $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}"));
    }

    public DailyReportSummaryInput DeserializeInput(string serializedOrchestraionInput)
    {
        return JsonSerializer.Deserialize<DailyReportSummaryAgentInput>(serializedOrchestraionInput).ThrowIfNull().Input;
    }
}

