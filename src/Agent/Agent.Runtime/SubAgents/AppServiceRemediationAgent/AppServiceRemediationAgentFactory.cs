// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Definitions;
using Agent.Plugins;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask;
using System.Text.Json;
using Agent.Core;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.Communication;

namespace Agent.Runtime.SubAgents.AppServiceRemediation;

// [Export]
public sealed class AppServiceRemediationAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IThreadOrchestrationManager _mappingManager;

    public const string OrchestrationInstanceIdPrefix = nameof(AppServiceRemediationAgent);

    public AppServiceRemediationAgentFactory(
        IMetricsPlugin metricsPlugin,
        IApprovalPlugin approvalPlugin,
        ITimePlugin timePlugin,
        IRemediationPlugin remediationPlugin,
        IRecordActionsPlugin recordActionsPlugin,
        IChartPlugin chartPlugin,
        ToolsRepository toolsRepository,
        IThreadOrchestrationManager mappingManager,
        DurableTaskClient durableTaskClient)
    {
        var toolSignatures = new List<string>();
        var timePluginDefinition = new TimePluginDefinition(timePlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => timePluginDefinition.GetCurrentUtcTime));
        toolSignatures.Add(ToolsRepository.GetSignature(() => timePluginDefinition.GetAppTimeZone));

        var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
        // TODO: use StartGetXXX once we have DTS version of the plugin
        toolSignatures.Add(ToolsRepository.GetSignature(() => metricsPluginDefinition.GetWebAppCpuMetrics));
        toolSignatures.Add(ToolsRepository.GetSignature(() => metricsPluginDefinition.GetMemoryMetrics));
        toolSignatures.Add(ToolsRepository.GetSignature(() => metricsPluginDefinition.GetFunctionAppRequestAvailability));
        toolSignatures.Add(ToolsRepository.GetSignature(() => metricsPluginDefinition.GetSuccessfulRequestVolumeAsync));

        var chartPluginDefinition = new ChartPluginDefinition(chartPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => chartPluginDefinition.PlotTimeSeriesDataAsync));
        toolSignatures.Add(ToolsRepository.GetSignature(() => chartPluginDefinition.PlotPieChartAsync));
        toolSignatures.Add(ToolsRepository.GetSignature(() => chartPluginDefinition.PlotBarChartAsync));
        toolSignatures.Add(ToolsRepository.GetSignature(() => chartPluginDefinition.PlotScatterAsync));

        var remediationPluginDefinition = new RemediationPluginDefinition(remediationPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => remediationPluginDefinition.ScaleAppServicePlanVertically));
        toolSignatures.Add(ToolsRepository.GetSignature(() => remediationPluginDefinition.SuggestNextSku));
        toolSignatures.Add(ToolsRepository.GetSignature(() => remediationPluginDefinition.CalculateScalingCost));
        toolSignatures.Add(ToolsRepository.GetSignature(() => remediationPluginDefinition.RestartWebApp));
        toolSignatures.Add(ToolsRepository.GetSignature(() => remediationPluginDefinition.CollectMemoryDump));

        var recordActionsPluginDefinition = new RecordActionsPluginDefinition(recordActionsPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => recordActionsPluginDefinition.RecordAction));
        toolSignatures.Add(ToolsRepository.GetSignature(() => recordActionsPluginDefinition.GetActionDetails));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        var approvalPluginDefinition = new ApprovalPluginDefinition(approvalPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => approvalPluginDefinition.StartApprovalFlow));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
        _mappingManager = mappingManager;
    }

    public async Task<string> StartOrchestration(
        AppServiceRemediationInput input,
        ThreadContext context)
    {
        var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";
        var threadId = context.ThreadId.ToString();

        await _mappingManager.AddMappingAsync(threadId, instanceId);

        return await _durableTaskClient.ScheduleNewAppServiceRemediationAgentInstanceAsync(
            new AppServiceRemediationAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                context),
            new StartOrchestrationOptions(InstanceId: instanceId));
    }

    public AppServiceRemediationInput DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<AppServiceRemediationAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
    }
}

