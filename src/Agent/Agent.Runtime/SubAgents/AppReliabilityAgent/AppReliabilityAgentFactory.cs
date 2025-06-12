using Agent.Core.Models;
using Agent.Plugins.Definitions;
using Agent.Plugins;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask;
using System.Text.Json;
using Agent.Core;
using Agent.Core.Interfaces;
using Agent.Plugins.Interface;

namespace Agent.Runtime.SubAgents.AppReliabilityAgent;


// [Export]
public sealed class AppReliabilityAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IToolsRepository _toolsRepository;


    public const string OrchestrationInstanceIdPrefix = nameof(AppReliabilityAgent);

    public AppReliabilityAgentFactory(
        IMetricsPlugin metricsPlugin,
        IReliabilityPlugin reliabilityPlugin,
        IToolsRepository toolsRepository,
        DurableTaskClient durableTaskClient,
        IArmClientFactory armClientFactory)
    {
        _toolsRepository = toolsRepository;
        var toolSignatures = new List<string>();
        var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => metricsPluginDefinition.GetSuccessfulRequestVolumeAsync));

        var reliabilityPluginDefinition = new ReliabilityPluginDefinition(reliabilityPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => reliabilityPluginDefinition.UpdateAutoHeal));
        toolSignatures.Add(_toolsRepository.GetSignature(() => reliabilityPluginDefinition.UpdateAlwaysOn));
        toolSignatures.Add(_toolsRepository.GetSignature(() => reliabilityPluginDefinition.UpdateHealthCheck));
        toolSignatures.Add(_toolsRepository.GetSignature(() => reliabilityPluginDefinition.UpdateHostWorkers));
        toolSignatures.Add(_toolsRepository.GetSignature(() => reliabilityPluginDefinition.GetAppsToMonitor));
        toolSignatures.Add(_toolsRepository.GetSignature(() => reliabilityPluginDefinition.GetReliabilityOrchestrationStatus));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

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
