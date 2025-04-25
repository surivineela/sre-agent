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
    private readonly IToolsRepository _toolsRepository;
    private readonly IThreadOrchestrationManager _mappingManager;
    public const string OrchestrationInstanceIdPrefix = nameof(DailyReportSummaryAgent);

    public DailyReportSummaryAgentFactory(
        IMetricsPlugin metricsPlugin,
        IGrafanaPlugin grafanaPlugin,
        IRecordActionsPlugin recordActionsPlugin,
        IGraphDBPlugin graphDBPlugin,
        IToolsRepository toolsRepository,
        IThreadOrchestrationManager mappingManager,
        DurableTaskClient durableTaskClient)
    {
        _toolsRepository = toolsRepository;
        var toolSignatures = new List<string>();

        // Metrics plugin for basic telemetry
        var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => metricsPluginDefinition.GetSuccessfulRequestVolumeAsync));

        // Grafana plugin for dashboard and visualization
        var grafanaPluginDefinition = new GrafanaPluginDefinition(grafanaPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => grafanaPluginDefinition.ModifyGrafanaDashboard));

        var graphDBPluginDefinition = new GraphDBPluginDefinition(graphDBPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => graphDBPluginDefinition.VisualizeApplicationComponents));
        toolSignatures.Add(_toolsRepository.GetSignature(() => graphDBPluginDefinition.DiscoverApplications));
        toolSignatures.Add(_toolsRepository.GetSignature(() => graphDBPluginDefinition.GetApplicationComponentsSummary));
        toolSignatures.Add(_toolsRepository.GetSignature(() => graphDBPluginDefinition.FindAllNetworkConnectedResources));

        // Control flow plugin for basic orchestration functions
        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

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

