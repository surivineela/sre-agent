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
    private readonly IToolsRepository _toolsRepository;
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
        INSGRulePlugin nSGRulePlugin,
        IThreadOrchestrationManager mappingManager,
        IToolsRepository toolsRepository,
        DurableTaskClient durableTaskClient)
    {
        _toolsRepository = toolsRepository;
        var toolSignatures = new List<string>();
        var timePluginDefinition = new TimePluginDefinition(timePlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => timePluginDefinition.GetCurrentUtcTime));
        toolSignatures.Add(_toolsRepository.GetSignature(() => timePluginDefinition.GetAppTimeZone));

        var containerAppPluginDefinition = new ContainerAppPluginDefinition(containerAppPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => containerAppPluginDefinition.ListRevisionsAsync));
        toolSignatures.Add(_toolsRepository.GetSignature(() => containerAppPluginDefinition.GetContainerAppRequestMetrics));
        toolSignatures.Add(_toolsRepository.GetSignature(() => containerAppPluginDefinition.GetContainerAppMemoryMetrics));
        //toolSignatures.Add(_toolsRepository.GetSignature(() => containerAppPluginDefinition.GetContainerAppCpuMetrics));
        toolSignatures.Add(_toolsRepository.GetSignature(() => containerAppPluginDefinition.GetContainerAppInfoAsync));
        toolSignatures.Add(_toolsRepository.GetSignature(() => containerAppPluginDefinition.GetLatestRevisionAsync));
        toolSignatures.Add(_toolsRepository.GetSignature(() => containerAppPluginDefinition.ListContainerAppsAsync));
        toolSignatures.Add(_toolsRepository.GetSignature(() => containerAppPluginDefinition.RestartContainerApp));
        toolSignatures.Add(_toolsRepository.GetSignature(() => containerAppPluginDefinition.GetAllNSGRulesForContainerAppAsync));
        toolSignatures.Add(_toolsRepository.GetSignature(() => containerAppPluginDefinition.ScaleContainerApp));
        toolSignatures.Add(_toolsRepository.GetSignature(() => containerAppPluginDefinition.GetRevisionLogsAsync));
        toolSignatures.Add(_toolsRepository.GetSignature(() => containerAppPluginDefinition.GetContainerAppLogsAsync));
        toolSignatures.Add(_toolsRepository.GetSignature(() => containerAppPluginDefinition.UpdateTargetPort));
        toolSignatures.Add(_toolsRepository.GetSignature(() => containerAppPluginDefinition.ListAvailableScalers));
        toolSignatures.Add(_toolsRepository.GetSignature(() => containerAppPluginDefinition.GetScalerDetails));

        var nsgRulePluginDefinition = new NSGRulePluginDefinition(nSGRulePlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => nsgRulePluginDefinition.CreateOrUpdateNSGRuleAsync));
        toolSignatures.Add(_toolsRepository.GetSignature(() => nsgRulePluginDefinition.RemoveNSGRuleAsync));

        var graphDBPluginDefinition = new GraphDBPluginDefinition(graphDbPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => graphDBPluginDefinition.FindAllNetworkConnectedResources));

        var chartPluginDefinition = new ChartPluginDefinition(chartPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => chartPluginDefinition.PlotTimeSeriesData));
        toolSignatures.Add(_toolsRepository.GetSignature(() => chartPluginDefinition.PlotPieChartAsync));
        toolSignatures.Add(_toolsRepository.GetSignature(() => chartPluginDefinition.PlotBarChartAsync));
        toolSignatures.Add(_toolsRepository.GetSignature(() => chartPluginDefinition.PlotScatterAsync));

        var recordActionsPluginDefinition = new RecordActionsPluginDefinition(recordActionsPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => recordActionsPluginDefinition.RecordAction));
        toolSignatures.Add(_toolsRepository.GetSignature(() => recordActionsPluginDefinition.GetActionDetails));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        var approvalPluginDefinition = new ApprovalPluginDefinition(approvalPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => approvalPluginDefinition.StartApprovalFlow));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
        _mappingManager = mappingManager;
    }

    public async Task<string> StartOrchestration(
        string input,
        Guid threadId)
    {
        var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";

        await _mappingManager.AddMappingAsync(threadId.ToString(), instanceId);
        return await _durableTaskClient.ScheduleNewContainerAppsRemediationAgentInstanceAsync(
            new ContainerAppsRemediationAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                ThreadId: threadId),
            new StartOrchestrationOptions(InstanceId: instanceId));
    }

    public string DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<ContainerAppsRemediationAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
    }
}
