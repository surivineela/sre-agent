using System.Text.Json;
using Agent.Core;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Plugins.Implementation;
using Agent.Plugins.Interface;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace Agent.Runtime.SubAgents.FunctionAppDeploymentChecksAgent;

/// <summary>
/// Factory for creating Function App Deployment Checks Agents
/// </summary>
public sealed class FunctionAppDeploymentChecksAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IToolsRepository _toolsRepository;

    /// <summary>
    /// Prefix for orchestration instances
    /// </summary>
    public const string OrchestrationInstanceIdPrefix = nameof(FunctionAppDeploymentChecksAgentFactory);

    /// <summary>
    /// Constructor for FunctionAppDeploymentChecksAgentFactory
    /// </summary>
    public FunctionAppDeploymentChecksAgentFactory(
        IToolsRepository toolsRepository,
        DurableTaskClient durableTaskClient,
        IArmPlugin armPlugin,
        IFunctionAppDeploymentChecksPlugin functionAppDeploymentChecksPlugin,
        IFunctionAppExecutionFailuresPlugin functionAppExecutionFailuresPlugin,
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

        var functionAppDeploymentChecksPluginDefinition = new FunctionAppDeploymentChecksPluginDefinition(functionAppDeploymentChecksPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => functionAppDeploymentChecksPluginDefinition.GetFunctionAppDeploymentChecks));
        toolSignatures.Add(_toolsRepository.GetSignature(() => functionAppDeploymentChecksPluginDefinition.GetFunctionAppDeploymentHistory));

        var functionAppExecutionFailuresPluginDefinition = new FunctionAppExecutionFailuresPluginDefinition(functionAppExecutionFailuresPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => functionAppExecutionFailuresPluginDefinition.GetFunctionAppCallStacks));

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
        return await _durableTaskClient.ScheduleNewFunctionAppDeploymentChecksAgentInstanceAsync(
            new FunctionAppDeploymentChecksAgentInput(
                FunctionAppResourceId: functionAppResourceId,
                ToolSignatures: _toolSignatures,
                ThreadId: threadId),
            new StartOrchestrationOptions(InstanceId: $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}"));
    }

    /// <summary>
    /// Deserialize the input
    /// </summary>
    public FunctionAppDeploymentChecksAgentInput DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<FunctionAppDeploymentChecksAgentInput>(serializedOrchestrationInput).ThrowIfNull();
    }
}
