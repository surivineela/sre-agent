using System.Text.Json;
using Agent.Core;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.HelperAgents;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace Agent.Runtime.SubAgents.WebAppDownAgent;

public sealed class WebAppDownAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;

    public const string OrchestrationInstanceIdPrefix = nameof(WebAppDownAgent);

    public WebAppDownAgentFactory(
        DurableTaskClient durableTaskClient)
    {
        var registry = new AgentToolsRegistry();

        registry.RegisterTool<MetricsPluginDefinition>(x => x.GetFunctionAppRequestAvailability);
        registry.RegisterTool<MetricsPluginDefinition>(x => x.GetWebAppCpuMetrics);
        registry.RegisterTool<MetricsPluginDefinition>(x => x.GetMemoryMetrics);
        registry.RegisterTool<MetricsPluginDefinition>(x => x.GetThreadMetrics);

        registry.RegisterTool<GitHubIssuePluginDefinition>(x => x.CreateGithubIssue);
        registry.RegisterTool<GitHubIssuePluginDefinition>(x => x.FetchGithubIssue);
        registry.RegisterTool<GitHubIssuePluginDefinition>(x => x.FindConnectedRepo);

        registry.RegisterPlugin<ControlFlowPluginDefinition>();

        registry.RegisterTool<ChartPluginDefinition>(x => x.PlotTimeSeriesData);
        registry.RegisterTool<ChartPluginDefinition>(x => x.PlotBarChartAsync);
        registry.RegisterTool<ChartPluginDefinition>(x => x.PlotScatterAsync);

        registry.RegisterTool<AppCodeAnalysisPluginDefinition>(x => x.PerformDeploymentSwapForApp);
        registry.RegisterTool<AppCodeAnalysisPluginDefinition>(x => x.GetDeploymentActivity);
        registry.RegisterTool<AppCodeAnalysisPluginDefinition>(x => x.GetCallStackForApp);
        registry.RegisterTool<AppCodeAnalysisPluginDefinition>(x => x.GetSummaryOfExceptions);
        registry.RegisterTool<AppCodeAnalysisPluginDefinition>(x => x.GetStackTraceOfLastException);
        registry.RegisterTool<AppCodeAnalysisPluginDefinition>(x => x.GetStackTraceOfMostCommonException);
        registry.RegisterTool<AppCodeAnalysisPluginDefinition>(x => x.GetAppConsoleLogs);

        registry.RegisterPlugin<CpuAnalysisPluginDefinition>();

        registry.RegisterTool<DotnetAnalysisPluginDefinition>(x => x.GetMemoryAnalysis);
        registry.RegisterTool<DotnetAnalysisPluginDefinition>(x => x.GetCPUAnalysis);
        registry.RegisterTool<DotnetAnalysisPluginDefinition>(x => x.ShouldTriggerMemoryDump);

        registry.RegisterTool<HelperAgentsPluginDefinition>(x => x.StartDiagnosisAgent);

        _toolSignatures = registry.ToolSignatures;
        _durableTaskClient = durableTaskClient;
    }

    public async Task<string> StartOrchestration(
        string resourceId,
        Guid threadId)
    {
        var instanceId = $"{OrchestrationInstanceIdPrefix}-{threadId}-{DateTime.Now:yyyyMMdd-HHmmss}";

        return await _durableTaskClient.ScheduleNewWebAppDownAgentInstanceAsync(
            new WebAppDownAgentInput(
                Input: resourceId,
                ToolSignatures: _toolSignatures,
                ThreadId: threadId,
                HelperAgentsInputs: GetHelperAgentInputs()),
            new StartOrchestrationOptions(InstanceId: $"{instanceId}"));
    }

    public string DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<WebAppDownAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
    }

    private static IReadOnlyList<HelperAgentInput> GetHelperAgentInputs()
    {
        var diagnosticAgentTools = new DiagnosisAgentToolsRegistry();

        diagnosticAgentTools.RegisterReadOnlyTool<GraphDBPluginDefinition>(x => x.GetResourceDetailedProperties);
        diagnosticAgentTools.RegisterReadOnlyTool<GraphDBPluginDefinition>(x => x.GetApplicationComponentsSummary);
        diagnosticAgentTools.RegisterReadOnlyTool<MetricsPluginDefinition>(x => x.GetFunctionAppRequestAvailability);
        diagnosticAgentTools.RegisterReadOnlyTool<MetricsPluginDefinition>(x => x.GetWebAppCpuMetrics);
        diagnosticAgentTools.RegisterReadOnlyTool<MetricsPluginDefinition>(x => x.GetMemoryMetrics);
        diagnosticAgentTools.RegisterReadOnlyTool<MetricsPluginDefinition>(x => x.GetThreadMetrics);
        diagnosticAgentTools.RegisterReadOnlyTool<AppCodeAnalysisPluginDefinition>(x => x.GetDeploymentActivity);
        diagnosticAgentTools.RegisterReadOnlyTool<AppCodeAnalysisPluginDefinition>(x => x.GetSummaryOfExceptions);
        diagnosticAgentTools.RegisterReadOnlyTool<AppCodeAnalysisPluginDefinition>(x => x.GetStackTraceOfLastException);
        diagnosticAgentTools.RegisterReadOnlyTool<AppCodeAnalysisPluginDefinition>(x => x.GetStackTraceOfMostCommonException);
        diagnosticAgentTools.RegisterReadOnlyTool<AppCodeAnalysisPluginDefinition>(x => x.GetAppConsoleLogs);
        diagnosticAgentTools.RegisterReadOnlyPlugin<CpuAnalysisPluginDefinition>();
        diagnosticAgentTools.RegisterReadOnlyPlugin<DotnetAnalysisPluginDefinition>();

        var diagnosticAgentInput = new DiagnosisAgentInput
        {
            ToolSignatures = diagnosticAgentTools.ToolSignatures,
            CustomInstructions = """
            You will be asked to investigate a failing web app. The provided tools are specialized for retrieving information about web app resource.

            # Environment Understanding
            Each web app is hosted in an "app service plan". The app service plan has a SKU which describes the CPU and memory resources available to the app.
            All apps in the same app service plan share the same resources. So if one app is consuming all the resources, other apps in the same plan may be affected.
            You can use the GetResourceDetailedProperties tool to get info about them. GetApplicationComponentsSummary returns related resources to any resource.

            Examples of issues that may impact an app are:
            - Application Code Bug (eg: unhandled exceptions, infinite loops, memory leaks)
            - Resource constraint (eg: high request volume leads to high CPU or memory)
            - Network issues (eg: DNS resolution failure, network security group misconfiguration)

            GetFunctionAppRequestAvailability tool can be used to find out if and when app experienced issues.
            Application Logs are good indicators of application issues (exceptions). Use GetAppConsoleLogs, GetSummaryOfExceptions etc tools to gather this info.
            Deployment Activity is a good indicator of recent changes to application code. Use GetDeploymentActivity to gather this info.
            CPU and Memory metrics are good indicators of resource constraints. You should be aware of the CPU and Memory available to app to know what is "high memory usage".
            Use GetMemoryMetrics, GetWebAppCpuMetrics etc tools to gather this info. And the corresponding analysis tools to find issues with the app.
            All these signals contain timestamped data, so you can correlate them to find the root cause of the issue.

            Potential mitigation options are to scale up the app, revert the deployment slot etc.
            Consider real-world impact of these actions to create a safe and comprehensive mitgation plan.
            """
        };

        return [diagnosticAgentInput];
    }
}
