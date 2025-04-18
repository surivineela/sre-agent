// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core;
using Agent.Core.Models.Api.v1;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.Communication;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace Agent.Runtime.SubAgents.ContainerAppsRemediation;

// [Export]
public sealed class ContainerAppsRemediationAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IThreadOrchestrationManager _mappingManager;

    public const string OrchestrationInstanceIdPrefix = nameof(ContainerAppsRemediationAgent);

    public ContainerAppsRemediationAgentFactory(
        IContainerAppPlugin containerAppPlugin,
        IArmPlugin armPlugin,
        IApprovalPlugin approvalPlugin,
        ITimePlugin timePlugin,
        IRemediationPlugin remediationPlugin,
        IRecordActionsPlugin recordActionsPlugin,
        IGraphDBPlugin graphDbPlugin,
        IChartPlugin chartPlugin,
        IThreadOrchestrationManager mappingManager,
        DurableTaskClient durableTaskClient)
    {
        var toolSignatures = new List<string>();
        var timePluginDefinition = new TimePluginDefinition(timePlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => timePluginDefinition.GetCurrentUtcTime));
        toolSignatures.Add(ToolsRepository.GetSignature(() => timePluginDefinition.GetAppTimeZone));

        var containerAppPluginDefinition = new ContainerAppPluginDefinition(containerAppPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => containerAppPluginDefinition.ListRevisionsAsync));
        toolSignatures.Add(ToolsRepository.GetSignature(() => containerAppPluginDefinition.GetContainerAppRequestMetrics));
        toolSignatures.Add(ToolsRepository.GetSignature(() => containerAppPluginDefinition.GetContainerAppMemoryMetrics));
        //toolSignatures.Add(ToolsRepository.GetSignature(() => containerAppPluginDefinition.GetContainerAppCpuMetrics));
        toolSignatures.Add(ToolsRepository.GetSignature(() => containerAppPluginDefinition.GetContainerAppInfoAsync));
        toolSignatures.Add(ToolsRepository.GetSignature(() => containerAppPluginDefinition.GetLatestRevisionAsync));
        toolSignatures.Add(ToolsRepository.GetSignature(() => containerAppPluginDefinition.ListContainerAppsAsync));
        toolSignatures.Add(ToolsRepository.GetSignature(() => containerAppPluginDefinition.RestartContainerApp));
        toolSignatures.Add(ToolsRepository.GetSignature(() => containerAppPluginDefinition.GetAllNSGRulesForContainerAppAsync));
        toolSignatures.Add(ToolsRepository.GetSignature(() => containerAppPluginDefinition.CreateOrUpdateNSGRuleAsync));
        toolSignatures.Add(ToolsRepository.GetSignature(() => containerAppPluginDefinition.RemoveNSGRuleAsync));
        toolSignatures.Add(ToolsRepository.GetSignature(() => containerAppPluginDefinition.ScaleContainerApp));

        var graphDBPluginDefinition = new GraphDBPluginDefinition(graphDbPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => graphDBPluginDefinition.FindAllNetworkConnectedResources));

        var chartPluginDefinition = new ChartPluginDefinition(chartPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => chartPluginDefinition.PlotTimeSeriesDataAsync));
        toolSignatures.Add(ToolsRepository.GetSignature(() => chartPluginDefinition.PlotPieChartAsync));
        toolSignatures.Add(ToolsRepository.GetSignature(() => chartPluginDefinition.PlotBarChartAsync));
        toolSignatures.Add(ToolsRepository.GetSignature(() => chartPluginDefinition.PlotScatterAsync));

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
        string input,
        ThreadContext context)
    {
        var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";

        var threadId = context.ThreadId.ToString();

        await _mappingManager.AddMappingAsync(threadId, instanceId);
        return await _durableTaskClient.ScheduleNewContainerAppsRemediationAgentInstanceAsync(
            new ContainerAppsRemediationAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                context),
            new StartOrchestrationOptions(InstanceId: instanceId));
    }

    public string DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<ContainerAppsRemediationAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
    }
}
