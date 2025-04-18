using Agent.Core.Models;
using Agent.Plugins.Definitions;
using Agent.Plugins;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using Agent.Core;
using Agent.Core.Models.Api.v1;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Agent.Plugins.Implementation;

namespace Agent.Runtime.SubAgents.AppReliabilityAgent;


// [Export]
public sealed class AppReliabilityAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;

    public const string OrchestrationInstanceIdPrefix = nameof(AppReliabilityAgent);

    public AppReliabilityAgentFactory(
        IMetricsPlugin metricsPlugin,
        IReliabilityPlugin reliabilityPlugin,
        IApprovalPlugin approvalPlugin,
        ToolsRepository toolsRepository,
        DurableTaskClient durableTaskClient,
        IArmClientFactory armClientFactory)
    {
        var toolSignatures = new List<string>();
        var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => metricsPluginDefinition.GetSuccessfulRequestVolumeAsync));

        var reliabilityPluginDefinition = new ReliabilityPluginDefinition(reliabilityPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => reliabilityPluginDefinition.UpdateAutoHeal));
        toolSignatures.Add(ToolsRepository.GetSignature(() => reliabilityPluginDefinition.UpdateAlwaysOn));
        toolSignatures.Add(ToolsRepository.GetSignature(() => reliabilityPluginDefinition.UpdateHealthCheck));
        toolSignatures.Add(ToolsRepository.GetSignature(() => reliabilityPluginDefinition.UpdateHostWorkers));
        toolSignatures.Add(ToolsRepository.GetSignature(() => reliabilityPluginDefinition.GetAppsToMonitor));
        toolSignatures.Add(ToolsRepository.GetSignature(() => reliabilityPluginDefinition.GetReliabilityOrchestrationStatus));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        //var approvalPluginDefinition = new ApprovalPluginDefinition(approvalPlugin);
        //toolSignatures.Add(ToolsRepository.GetSignature(() => approvalPluginDefinition.StartApprovalFlow));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
    }

    public async Task<string> StartOrchestration(
        AppReliabilityInput input,
        Guid threadId)
    {
        return await _durableTaskClient.ScheduleNewAppReliabilityAgentInstanceAsync(
            new AppReliabilityAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                ThreadId: threadId),
            new StartOrchestrationOptions(InstanceId: $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}"));
    }

    public AppReliabilityInput DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<AppReliabilityAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
    }
}
