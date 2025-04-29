using System.Text.Json;
using Agent.Core;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Plugins.Implementation;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using OperationalAgentCore;

namespace Agent.Runtime.SubAgents.FunctionAppExecutionFailuresAgent;
public sealed class FunctionAppExecutionFailuresAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly ILogger<FunctionAppsPluginDefinition> _logger;

    public const string OrchestrationInstanceIdPrefix = nameof(FunctionAppExecutionFailuresAgentFactory);

    public FunctionAppExecutionFailuresAgentFactory(
        IToolsRepository toolsRepository,
        DurableTaskClient durableTaskClient,
        ILogger<FunctionAppsPluginDefinition> logger,
        IArmPlugin armPlugin,
        IFunctionAppExecutionFailuresPlugin functionAppExecutionFailuresPlugin,
        IFunctionAppsPlugin functionAppsPlugin,
        IGithubIssuePlugin githubPlugin,
        IAppCodeAnalysisPlugin appCodeAnalysisPlugin,
        IMetricsPlugin metricsPlugin
        )
    {
        _logger = logger;
        var toolSignatures = new List<string>();

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        var functionAppsPluginDefinition = new FunctionAppsPluginDefinition(functionAppsPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => functionAppsPluginDefinition.ListFunctionAppsAsync));

        var armPluginDefinition = new ArmPluginDefinition(armPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => armPluginDefinition.GetArmResourceAsJson));

        var githubPluginDefinition = new GitHubIssuePluginDefinition(githubPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => githubPluginDefinition.CreateGithubIssue));

        var appCodeAnalysisPluginDefinition = new AppCodeAnalysisPluginDefinition(appCodeAnalysisPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.WaitInMilliSeconds));

        var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => metricsPluginDefinition.GetFunctionAppRequestAvailability));

        var functionAppExecutionFailuresPluginDefinition = new FunctionAppExecutionFailuresPluginDefinition(functionAppExecutionFailuresPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => functionAppExecutionFailuresPluginDefinition.GetFunctionAppExecutionFailures));
        toolSignatures.Add(toolsRepository.GetSignature(() => functionAppExecutionFailuresPluginDefinition.GetFunctionAppCallStacks));
        toolSignatures.Add(toolsRepository.GetSignature(() => functionAppExecutionFailuresPluginDefinition.GetFailedRequestsPerFunction));
        toolSignatures.Add(toolsRepository.GetSignature(() => functionAppExecutionFailuresPluginDefinition.GetHostRuntimeErrorEvents));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
    }

    public async Task<string> StartOrchestration(
       string functionAppResourceId,
       Guid threadId)
    {
        return await _durableTaskClient.ScheduleNewFunctionAppExecutionFailuresAgentInstanceAsync(
            new FunctionAppExecutionFailuresAgentInput(
                FunctionAppResourceId: functionAppResourceId,
                ToolSignatures: _toolSignatures,
                ThreadId: threadId),
            new StartOrchestrationOptions(InstanceId: $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}"));
    }

    public FunctionAppExecutionFailuresAgentInput DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<FunctionAppExecutionFailuresAgentInput>(serializedOrchestrationInput).ThrowIfNull();
    }
}
