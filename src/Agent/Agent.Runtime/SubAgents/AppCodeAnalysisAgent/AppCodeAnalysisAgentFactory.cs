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

    public const string OrchestrationInstanceIdPrefix = nameof(AppCodeAnalysisAgent);

    public AppCodeAnalysisAgentFactory(
        IApprovalPlugin approvalPlugin,
        IMetricsPlugin metricsPlugin,
        IGithubIssuePlugin githubPlugin,
        IChartPlugin chartPlugin,
        ToolsRepository toolsRepository,
        DurableTaskClient durableTaskClient,
        IAppCodeAnalysisPlugin appCodeAnalysisPlugin)
    {
        //change tool signatures 
        var toolSignatures = new List<string>();

        var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => metricsPluginDefinition.GetFunctionAppRequestAvailability));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        var chartPluginDefinition = new ChartPluginDefinition(chartPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => chartPluginDefinition.PlotTimeSeriesDataAsync));

        //var approvalPluginDefinition = new ApprovalPluginDefinition(approvalPlugin);
        //toolSignatures.Add(ToolsRepository.GetSignature(() => approvalPluginDefinition.StartApprovalFlow));

        var githubPluginDefinition = new GitHubIssuePluginDefinition(githubPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => githubPluginDefinition.CreateGithubIssue));

        var appCodeAnalysisPluginDefinition = new AppCodeAnalysisPluginDefinition(appCodeAnalysisPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.GetCallStackForApp));
        toolSignatures.Add(ToolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.PerformDeploymentSwapForApp));
        toolSignatures.Add(ToolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.GetDeploymentActivity));

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
