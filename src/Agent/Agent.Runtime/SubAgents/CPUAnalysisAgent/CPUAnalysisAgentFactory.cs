using Agent.Core;
using Agent.Plugins.Definitions;
using Agent.Plugins;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask;
using System.Text.Json;
using Agent.Plugins.Interface;

namespace Agent.Runtime.SubAgents.CPUAnalysisAgent;

public sealed class CPUAnalysisAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IToolsRepository _toolsRepository;

    public const string OrchestrationInstanceIdPrefix = nameof(CPUAnalysisAgent);

    public CPUAnalysisAgentFactory(
        IMetricsPlugin metricsPlugin,
        IGithubIssuePlugin githubPlugin,
        IToolsRepository toolsRepository,
        DurableTaskClient durableTaskClient,
        ICpuAnalysisPlugin cpuAnalysisPlugin,
        IAppCodeAnalysisPlugin appCodeAnalysisPlugin)
    {
        _toolsRepository = toolsRepository;
        var toolSignatures = new List<string>();
        var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => metricsPluginDefinition.GetWebAppCpuMetrics));
        toolSignatures.Add(_toolsRepository.GetSignature(() => metricsPluginDefinition.GetFunctionAppRequestAvailability));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        var githubPluginDefinition = new GitHubIssuePluginDefinition(githubPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => githubPluginDefinition.CreateGithubIssue));

        var cpuAnalysisPluginDefinition = new CpuAnalysisPluginDefinition(cpuAnalysisPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => cpuAnalysisPluginDefinition.ScaleUpAppServicePlanBySku));
        toolSignatures.Add(_toolsRepository.GetSignature(() => cpuAnalysisPluginDefinition.AutoScaleApp));

        var appCodeAnalysisPluginDefinition = new AppCodeAnalysisPluginDefinition(appCodeAnalysisPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.WaitInMilliSeconds));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
    }

    public async Task<string> StartOrchestration(
        CPUAnalysisInput input,
        Guid threadId)
    {
        return await _durableTaskClient.ScheduleNewCPUAnalysisAgentInstanceAsync(
            new CPUAnalysisAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                ThreadId: threadId),
            new StartOrchestrationOptions(InstanceId: $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}"));
    }

    public CPUAnalysisInput DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<CPUAnalysisAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
    }
}
