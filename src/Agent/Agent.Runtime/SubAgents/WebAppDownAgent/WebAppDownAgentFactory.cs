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
using Agent.Runtime.Communication;

namespace Agent.Runtime.SubAgents.WebAppDownAgent;


// [Export]
public sealed class WebAppDownAgentFactory
{
    //private readonly AgentToolsRegistry _toolsRegistry = new AgentToolsRegistry();
    private readonly IReadOnlyList<string> _toolSignatures;

    private readonly DurableTaskClient _durableTaskClient;

    public const string OrchestrationInstanceIdPrefix = nameof(WebAppDownAgent);

    public WebAppDownAgentFactory(
        IGithubIssuePlugin githubIssuePlugin,
        IAppCodeAnalysisPlugin appCodeAnalysisPlugin,
        ICpuAnalysisPlugin cpuAnalysisPlugin,
        IMetricsPlugin metricsPlugin,
        IChartPlugin chartPlugin,
        DurableTaskClient durableTaskClient)
    {

        //_toolsRegistry.RegisterPlugin<MetricsPluginDefinition>();
        var toolSignatures = new List<string>();
        var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => metricsPluginDefinition.GetFunctionAppRequestAvailability));
        toolSignatures.Add(ToolsRepository.GetSignature(() => metricsPluginDefinition.GetWebAppCpuMetrics));
        toolSignatures.Add(ToolsRepository.GetSignature(() => metricsPluginDefinition.GetMemoryMetrics));
        toolSignatures.Add(ToolsRepository.GetSignature(() => metricsPluginDefinition.GetThreadMetrics)); 

        //_toolsRegistry.RegisterPlugin<GitHubIssuePluginDefinition>();
        var githubIssuePluginDefinition = new GitHubIssuePluginDefinition(githubIssuePlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => githubIssuePluginDefinition.CreateGithubIssue)); 

        //_toolsRegistry.RegisterPlugin<ControlFlowPluginDefinition>();
         var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        // toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput)); 

        //var approvalPluginDefinition = new ApprovalPluginDefinition(approvalPlugin);
        //toolSignatures.Add(toolsRepository.GetSignature(() => approvalPluginDefinition.StartApprovalFlow));

        //_toolsRegistry.RegisterTool<ChartPluginDefinition>(x => x.PlotTimeSeriesDataAsync);
         var chartPluginDefinition = new ChartPluginDefinition(chartPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => chartPluginDefinition.PlotTimeSeriesDataAsync)); 

        //_toolsRegistry.RegisterPlugin<AppCodeAnalysisPluginDefinition>();
        var appCodeAnalysisPluginDefinition = new AppCodeAnalysisPluginDefinition(appCodeAnalysisPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.PerformDeploymentSwapForApp));
        toolSignatures.Add(ToolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.GetDeploymentActivity));
        toolSignatures.Add(ToolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.GetCallStackForApp));
        toolSignatures.Add(ToolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.GetSummaryOfExceptions));
        toolSignatures.Add(ToolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.GetStackTraceOfLastException));
        toolSignatures.Add(ToolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.GetStackTraceOfMostCommonException));
        toolSignatures.Add(ToolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.WaitInMilliSeconds));
        toolSignatures.Add(ToolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.GetAppConsoleLogs));

        // _toolsRegistry.RegisterPlugin<CpuAnalysisPluginDefinition>();
        var cpuAnalysisPluginDefinition = new CpuAnalysisPluginDefinition(cpuAnalysisPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => cpuAnalysisPluginDefinition.CollectMemoryDumpForApp));
        toolSignatures.Add(ToolsRepository.GetSignature(() => cpuAnalysisPluginDefinition.ScaleUpAppServicePlanBySku));
        toolSignatures.Add(ToolsRepository.GetSignature(() => cpuAnalysisPluginDefinition.AutoScaleApp));

        //_mappingManager = mappingManager;
        _durableTaskClient = durableTaskClient;
        _toolSignatures = toolSignatures;
    }

    public async Task<string> StartOrchestration(
        WebAppDownInput input,
        Guid threadId)
    {
        var instanceId = $"{OrchestrationInstanceIdPrefix}-{threadId}-{DateTime.Now:yyyyMMdd-HHmmss}";

        //await _mappingManager.AddMappingAsync(threadId.ToString(), instanceId);

        return await _durableTaskClient.ScheduleNewWebAppDownAgentInstanceAsync(
            new WebAppDownAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                ThreadId: threadId),
            new StartOrchestrationOptions(InstanceId: $"{instanceId}"));
    }

    public WebAppDownInput DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<WebAppDownAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
    }
}
