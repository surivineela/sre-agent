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
using Agent.Core.Plugins;
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
        toolSignatures.Add(toolsRepository.GetSignature(() => metricsPluginDefinition.GetSuccessfulRequestVolumeAsync));

        var reliabilityPluginDefinition = new ReliabilityPluginDefinition(reliabilityPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => reliabilityPluginDefinition.UpdateAutoHeal));
        toolSignatures.Add(toolsRepository.GetSignature(() => reliabilityPluginDefinition.UpdateAlwaysOn));
        toolSignatures.Add(toolsRepository.GetSignature(() => reliabilityPluginDefinition.UpdateHealthCheck));
        toolSignatures.Add(toolsRepository.GetSignature(() => reliabilityPluginDefinition.UpdateHostWorkers));
        toolSignatures.Add(toolsRepository.GetSignature(() => reliabilityPluginDefinition.GetAppsToMonitor));
        toolSignatures.Add(toolsRepository.GetSignature(() => reliabilityPluginDefinition.GetReliabilityOrchestrationStatus));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        //var approvalPluginDefinition = new ApprovalPluginDefinition(approvalPlugin);
        //toolSignatures.Add(toolsRepository.GetSignature(() => approvalPluginDefinition.StartApprovalFlow));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
    }

    public async Task<string> StartOrchestration(
        AppReliabilityInput input,
        ThreadContext context)
    {
        return await _durableTaskClient.ScheduleNewAppReliabilityAgentInstanceAsync(
            new AppReliabilityAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                Context: context),
            new StartOrchestrationOptions(InstanceId: $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}"));
    }

    public AppReliabilityInput DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<AppReliabilityAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
    }
}
