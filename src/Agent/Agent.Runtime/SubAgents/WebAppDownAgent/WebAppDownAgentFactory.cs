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

namespace Agent.Runtime.SubAgents.WebAppDownAgent;


// [Export]
public sealed class WebAppDownAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;

    public const string OrchestrationInstanceIdPrefix = nameof(WebAppDownAgent);

    public WebAppDownAgentFactory(
        IMetricsPlugin metricsPlugin,
        IApprovalPlugin approvalPlugin,
        IChartPlugin chartPlugin,
        IPostToTeamsPlugin postToTeamsPlugin,
        IMetaAgentCPUAnalysisPlugin cpuPlugin,
        IMetaAgentAppCodeAnalysisPlugin appCodePlugin,
        DurableTaskClient durableTaskClient)
    {
        var toolSignatures = new List<string>();
        var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => metricsPluginDefinition.GetSuccessfulRequestVolumeAsync));
        toolSignatures.Add(ToolsRepository.GetSignature(() => metricsPluginDefinition.GetWebAppCpuMetrics));
        toolSignatures.Add(ToolsRepository.GetSignature(() => metricsPluginDefinition.GetMemoryMetrics));
        toolSignatures.Add(ToolsRepository.GetSignature(() => metricsPluginDefinition.GetThreadMetrics));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        var approvalPluginDefinition = new ApprovalPluginDefinition(approvalPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => approvalPluginDefinition.StartApprovalFlow));

        toolSignatures.Add(ToolsRepository.GetSignature(() => cpuPlugin.StartCPUAnalysisAgent));
        toolSignatures.Add(ToolsRepository.GetSignature(() => appCodePlugin.StartAppCodeAnalysisAgent));

        var postToTeamsPluginDefinition = new PostToTeamsPluginDefinition(postToTeamsPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => postToTeamsPluginDefinition.PostMessage));

        var chatPluginDefinition = new ChartPluginDefinition(chartPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => chatPluginDefinition.PlotTimeSeriesDataAsync));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
    }

    public async Task<string> StartOrchestration(
        WebAppDownInput input,
        ThreadContext context)
    {
        return await _durableTaskClient.ScheduleNewWebAppDownAgentInstanceAsync(
            new WebAppDownAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                Context: context),
            new StartOrchestrationOptions(InstanceId: $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}"));
    }

    public WebAppDownInput DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<WebAppDownAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
    }
}
