using Agent.Core.Models;
using Agent.Plugins.Definitions;
using Agent.Plugins;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask;
using System.Text.Json;
using Agent.Core;
using Agent.Core.Models.Api.v1;
using Agent.Core.Interfaces;
using Agent.Runtime.MetaAgent;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Agent.Core.Helpers;
using OperationalAgentCore;

namespace Agent.Runtime.SubAgents.AppCodeAnalysisAgent;


// [Export]
public sealed class AppCodeAnalysisAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IToolsRepository _toolsRepository;

    public const string OrchestrationInstanceIdPrefix = nameof(AppCodeAnalysisAgent);

    public AppCodeAnalysisAgentFactory(
        IMetricsPlugin metricsPlugin,
        IChartPlugin chartPlugin,
        IToolsRepository toolsRepository,
        IGithubIssuePlugin githubPlugin,
        DurableTaskClient durableTaskClient,
        IAppCodeAnalysisPlugin appCodeAnalysisPlugin)
    {
        _toolsRepository = toolsRepository;
        //change tool signatures 
        var toolSignatures = new List<string>();

        var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => metricsPluginDefinition.GetFunctionAppRequestAvailability));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));


        var chartPluginDefinition = new ChartPluginDefinition(chartPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => chartPluginDefinition.PlotTimeSeriesData));

        var githubPluginDefinition = new GitHubIssuePluginDefinition(githubPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => githubPluginDefinition.CreateGithubIssue));
        var appCodeAnalysisPluginDefinition = new AppCodeAnalysisPluginDefinition(appCodeAnalysisPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.GetCallStackForApp));
        toolSignatures.Add(_toolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.PerformDeploymentSwapForApp));
        toolSignatures.Add(_toolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.GetDeploymentActivity));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
    }


    public async Task<string> StartOrchestration(
        AppCodeAnalysisInput input,
        Guid threadId)
    {
        return await _durableTaskClient.ScheduleNewAppCodeAnalysisAgentInstanceAsync(
            new AppCodeAnalysisAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                ThreadId: threadId),
            new StartOrchestrationOptions(InstanceId: $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}"));
    }

    public AppCodeAnalysisInput DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<AppCodeAnalysisAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
    }


}
