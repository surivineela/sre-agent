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
        ToolsRepository toolsRepository,
        DurableTaskClient durableTaskClient)
    {
        var toolSignatures = new List<string>();
        var timePluginDefinition = new TimePluginDefinition(timePlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => timePluginDefinition.GetCurrentUtcTime));
        toolSignatures.Add(toolsRepository.GetSignature(() => timePluginDefinition.GetAppTimeZone));

        var containerAppPluginDefinition = new ContainerAppPluginDefinition(containerAppPlugin);
        // TODO: use StartGetXXX once we have DTS version of the plugin
        toolSignatures.Add(toolsRepository.GetSignature(() => containerAppPluginDefinition.GetContainerAppRequestMetrics));
        toolSignatures.Add(toolsRepository.GetSignature(() => containerAppPluginDefinition.GetContainerAppMemoryMetrics));
        //toolSignatures.Add(toolsRepository.GetSignature(() => containerAppPluginDefinition.GetContainerAppCpuMetrics));
        toolSignatures.Add(toolsRepository.GetSignature(() => containerAppPluginDefinition.GetContainerAppInfoAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => containerAppPluginDefinition.GetLatestRevisionAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => containerAppPluginDefinition.ListContainerAppsAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => containerAppPluginDefinition.RestartContainerApp));
        toolSignatures.Add(toolsRepository.GetSignature(() => containerAppPluginDefinition.GetAllNSGRulesForContainerAppAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => containerAppPluginDefinition.CreateOrUpdateNSGRuleAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => containerAppPluginDefinition.RemoveNSGRuleAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => containerAppPluginDefinition.ScaleContainerApp));

        var graphDBPluginDefinition = new GraphDBPluginDefinition(graphDbPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => graphDBPluginDefinition.FindAllNetworkConnectedResources));

        var chartPluginDefinition = new ChartPluginDefinition(chartPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => chartPluginDefinition.PlotTimeSeriesDataAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => chartPluginDefinition.PlotPieChartAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => chartPluginDefinition.PlotBarChartAsync));

        var recordActionsPluginDefinition = new RecordActionsPluginDefinition(recordActionsPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => recordActionsPluginDefinition.RecordAction));
        toolSignatures.Add(toolsRepository.GetSignature(() => recordActionsPluginDefinition.GetActionDetails));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        var approvalPluginDefinition = new ApprovalPluginDefinition(approvalPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => approvalPluginDefinition.StartApprovalFlow));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
    }

    public async Task<string> StartOrchestration(
        string input,
        string threadId = "")
    {
        var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";

        if (threadId != null)
        {
            await _mappingManager.AddMappingAsync(new ThreadOrchestrationMapping(
                Id: $"mapping_{threadId}",
                ThreadId: threadId,
                OrchestrationInstanceId: instanceId,
                CreatedTimestamp: DateTime.UtcNow,
                ModifiedTimestamp: DateTime.UtcNow
                )
            );
        }

        return await _durableTaskClient.ScheduleNewContainerAppsRemediationAgentInstanceAsync(
            new ContainerAppsRemediationAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                threadId),
            new StartOrchestrationOptions(InstanceId: instanceId));
    }

    public string DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<ContainerAppsRemediationAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
    }
}
