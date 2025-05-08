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
    private readonly IToolsRepository _toolsRepository;
    private readonly DurableTaskClient _durableTaskClient;

    public const string OrchestrationInstanceIdPrefix = nameof(WebAppDownAgent);

    public WebAppDownAgentFactory(
        IGithubIssuePlugin githubPlugin,
        IAppCodeAnalysisPlugin appCodeAnalysisPlugin,
        ICpuAnalysisPlugin cpuAnalysisPlugin,
        IMetricsPlugin metricsPlugin,
        IChartPlugin chartPlugin,
        IDotnetAnalysisPlugin dotnetAnalysisPlugin,
        IToolsRepository toolsRepository,
        DurableTaskClient durableTaskClient)
    {

        _toolsRepository = toolsRepository;

        var toolSignatures = new List<string>();
        var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => metricsPluginDefinition.GetFunctionAppRequestAvailability));
        toolSignatures.Add(_toolsRepository.GetSignature(() => metricsPluginDefinition.GetWebAppCpuMetrics));
        toolSignatures.Add(_toolsRepository.GetSignature(() => metricsPluginDefinition.GetMemoryMetrics));
        toolSignatures.Add(_toolsRepository.GetSignature(() => metricsPluginDefinition.GetThreadMetrics)); 

        var githubIssuePluginDefinition = new GitHubIssuePluginDefinition(githubPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => githubIssuePluginDefinition.CreateGithubIssue));
        toolSignatures.Add(_toolsRepository.GetSignature(() => githubIssuePluginDefinition.FindConnectedRepo));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait)); 

        //_toolsRegistry.RegisterTool<ChartPluginDefinition>(x => x.PlotTimeSeriesDataAsync);
         var chartPluginDefinition = new ChartPluginDefinition(chartPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => chartPluginDefinition.PlotTimeSeriesData));
        toolSignatures.Add(_toolsRepository.GetSignature(() => chartPluginDefinition.PlotBarChartAsync));
        toolSignatures.Add(_toolsRepository.GetSignature(() => chartPluginDefinition.PlotScatterAsync));

        var appCodeAnalysisPluginDefinition = new AppCodeAnalysisPluginDefinition(appCodeAnalysisPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.PerformDeploymentSwapForApp));
        toolSignatures.Add(_toolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.GetDeploymentActivity));
        toolSignatures.Add(_toolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.GetCallStackForApp));
        toolSignatures.Add(_toolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.GetSummaryOfExceptions));
        toolSignatures.Add(_toolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.GetStackTraceOfLastException));
        toolSignatures.Add(_toolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.GetStackTraceOfMostCommonException));
        //toolSignatures.Add(_toolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.WaitInMilliSeconds));
        toolSignatures.Add(_toolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.GetAppConsoleLogs));

        var cpuAnalysisPluginDefinition = new CpuAnalysisPluginDefinition(cpuAnalysisPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => cpuAnalysisPluginDefinition.CollectMemoryDumpForApp));
        toolSignatures.Add(_toolsRepository.GetSignature(() => cpuAnalysisPluginDefinition.CollectProfileForApp));
        toolSignatures.Add(_toolsRepository.GetSignature(() => cpuAnalysisPluginDefinition.ScaleUpAppServicePlanBySku));
        toolSignatures.Add(_toolsRepository.GetSignature(() => cpuAnalysisPluginDefinition.AutoScaleApp));

        var dotnetAnalysisPluginDefinition = new DotnetAnalysisPluginDefinition(dotnetAnalysisPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => dotnetAnalysisPluginDefinition.GetMemoryAnalysis));
        toolSignatures.Add(_toolsRepository.GetSignature(() => dotnetAnalysisPluginDefinition.GetCPUAnalysis));
        //toolSignatures.Add(_toolsRepository.GetSignature(() => dotnetAnalysisPluginDefinition.GetGCCPUAnalysis));
        //_mappingManager = mappingManager;
        _durableTaskClient = durableTaskClient;
        _toolSignatures = toolSignatures;
    }

    public async Task<string> StartOrchestration(
        string resourceId,
        Guid threadId)
    {
        var instanceId = $"{OrchestrationInstanceIdPrefix}-{threadId}-{DateTime.Now:yyyyMMdd-HHmmss}";

        //await _mappingManager.AddMappingAsync(threadId.ToString(), instanceId);

        return await _durableTaskClient.ScheduleNewWebAppDownAgentInstanceAsync(
            new WebAppDownAgentInput(
                Input: resourceId,
                ToolSignatures: _toolSignatures,
                ThreadId: threadId),
            new StartOrchestrationOptions(InstanceId: $"{instanceId}"));
    }

    public string DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<WebAppDownAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
    }
}
