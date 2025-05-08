using System.Text.Json;
using Agent.Core;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace Agent.Runtime.SubAgents.FunctionAppConfigurationCheck;

/// <summary>
/// Factory for creating Function App Configuration Check Agents
/// </summary>
public sealed class FunctionAppConfigurationCheckAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IToolsRepository _toolsRepository;

    /// <summary>
    /// Prefix for orchestration instances
    /// </summary>
    public const string OrchestrationInstanceIdPrefix = nameof(FunctionAppConfigurationCheckAgentFactory);

    /// <summary>
    /// Constructor for FunctionAppConfigurationCheckAgentFactory
    /// </summary>
    public FunctionAppConfigurationCheckAgentFactory(
        IToolsRepository toolsRepository,
        DurableTaskClient durableTaskClient,
        IArmPlugin armPlugin,
        IFunctionAppConfigurationChecksPlugin functionAppConfigurationChecksPlugin,
        IMetricsPlugin metricsPlugin)
    {
        _toolsRepository = toolsRepository;
        _durableTaskClient = durableTaskClient;

        var toolSignatures = new List<string>();

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        var armPluginDefinition = new ArmPluginDefinition(armPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => armPluginDefinition.GetArmResourceAsJson));
        toolSignatures.Add(_toolsRepository.GetSignature(() => armPluginDefinition.GetAppSetting));
        toolSignatures.Add(_toolsRepository.GetSignature(() => armPluginDefinition.UpdateAppSettingsAsync));

        var functionAppConfigurationChecksPluginDefinition = new FunctionAppConfigurationChecksPluginDefinition(functionAppConfigurationChecksPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => functionAppConfigurationChecksPluginDefinition.GetFunctionAppConfigurationChecks));

        var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => metricsPluginDefinition.GetFunctionAppRequestAvailability));

        _toolSignatures = toolSignatures;
    }

    /// <summary>
    /// Start the orchestration
    /// </summary>
    public async Task<string> StartOrchestration(
        string functionAppResourceId,
        Guid threadId)
    {
        return await _durableTaskClient.ScheduleNewFunctionAppConfigurationCheckAgentInstanceAsync(
            new FunctionAppConfigurationCheckAgentInput(
                FunctionAppResourceId: functionAppResourceId,
                ToolSignatures: _toolSignatures,
                ThreadId: threadId),
            new StartOrchestrationOptions(InstanceId: $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}"));
    }

    /// <summary>
    /// Deserialize the input
    /// </summary>
    public FunctionAppConfigurationCheckAgentInput DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<FunctionAppConfigurationCheckAgentInput>(serializedOrchestrationInput).ThrowIfNull();
    }
}
