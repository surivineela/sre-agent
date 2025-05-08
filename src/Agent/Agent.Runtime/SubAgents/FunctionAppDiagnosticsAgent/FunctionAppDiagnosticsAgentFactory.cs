using System.Text.Json;
using Agent.Core;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.FunctionAppDiagnosticsAgent;
public sealed class FunctionAppDiagnosticsAgentFactory
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly ILogger<FunctionAppsPluginDefinition> _logger;

    public const string OrchestrationInstanceIdPrefix = nameof(FunctionAppDiagnosticsAgentFactory);

    // Agent type keys
    public const string FunctionAppDiagnosticsAgentKey = "FunctionAppDiagnosticsAgent";
    public const string FunctionAppConnectivityAgentKey = "FunctionAppConnectivityAgent";
    public const string FunctionAppExecutionFailuresAgentKey = "FunctionAppExecutionFailuresAgent";
    public const string FunctionAppConfigurationCheckAgentKey = "FunctionAppConfigurationCheckAgent";

    public FunctionAppDiagnosticsAgentFactory(
        IToolsRepository toolsRepository,
        DurableTaskClient durableTaskClient,
        ILogger<FunctionAppsPluginDefinition> logger,
        ILogger<ChartPluginV2> loggerChartPluginV2,
        IArmPlugin armPlugin,
        IFunctionAppExecutionFailuresPlugin functionAppExecutionFailuresPlugin,
        IFunctionAppsPlugin functionAppsPlugin,
        IGithubIssuePlugin githubPlugin,
        IAppCodeAnalysisPlugin appCodeAnalysisPlugin,
        IMetricsPlugin metricsPlugin,
        IRoleAssignmentPlugin roleAssignmentPlugin,
        IGraphDBPlugin graphDBPlugin,
        ChartPluginV2 chartPlugin,
        IFunctionAppConfigurationChecksPlugin functionAppConfigurationChecksPlugin
        )
    {
        _logger = logger;
        var toolSignaturesDictionary = new Dictionary<string, IReadOnlyList<string>>();

        // Create tool signatures for FunctionAppDiagnosticsAgent
        var diagnosticsAgentTools = new List<string>();

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        diagnosticsAgentTools.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        diagnosticsAgentTools.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        diagnosticsAgentTools.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        diagnosticsAgentTools.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        var functionAppsPluginDefinition = new FunctionAppsPluginDefinition(functionAppsPlugin);
        diagnosticsAgentTools.Add(toolsRepository.GetSignature(() => functionAppsPluginDefinition.ListFunctionAppsAsync));

        var armPluginDefinition = new ArmPluginDefinition(armPlugin);
        diagnosticsAgentTools.Add(toolsRepository.GetSignature(() => armPluginDefinition.GetArmResourceAsJson));

        var functionAppExecutionFailuresPluginDefinition = new FunctionAppExecutionFailuresPluginDefinition(functionAppExecutionFailuresPlugin);
        diagnosticsAgentTools.Add(toolsRepository.GetSignature(() => functionAppExecutionFailuresPluginDefinition.GetHostRuntimeErrorEvents));

        // Add chart plotting capabilities
        var chartPluginDefinition = new ChartPluginDefinition(chartPlugin);
        diagnosticsAgentTools.Add(toolsRepository.GetSignature(() => chartPluginDefinition.PlotTimeSeriesData));

        // Add FunctionAppDiagnosticsAgent tools to dictionary
        toolSignaturesDictionary[FunctionAppDiagnosticsAgentKey] = diagnosticsAgentTools;

        // Create tool signatures for FunctionAppConnectivityAgent
        var connectivityAgentTools = new List<string>
        {
            toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait),
            toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete),
            toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser),
            toolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput),
            toolsRepository.GetSignature(() => armPluginDefinition.GetArmResourceAsJson),
            toolsRepository.GetSignature(() => armPluginDefinition.CheckConnectivityViaConnectionString),
            toolsRepository.GetSignature(() => armPluginDefinition.CheckTcpConnectivity),
            toolsRepository.GetSignature(() => armPluginDefinition.CheckDnsResolution),
            toolsRepository.GetSignature(() => armPluginDefinition.GetAppSetting),
            toolsRepository.GetSignature(() => armPluginDefinition.ListKeysForStorageAsync),
            toolsRepository.GetSignature(() => armPluginDefinition.UpdateAppSettingsAsync)
        };

        // Add chart plotting capabilities to connectivity agent
        connectivityAgentTools.Add(toolsRepository.GetSignature(() => chartPluginDefinition.PlotTimeSeriesData));

        var roleAssignmentPluginDefinition = new RoleAssignmentPluginDefinition(roleAssignmentPlugin);
        connectivityAgentTools.Add(toolsRepository.GetSignature(() => roleAssignmentPluginDefinition.AddRoleAssignment));
        connectivityAgentTools.Add(toolsRepository.GetSignature(() => roleAssignmentPluginDefinition.RemoveRoleAssignment));
        connectivityAgentTools.Add(toolsRepository.GetSignature(() => roleAssignmentPluginDefinition.CheckRoleAssignment));
        connectivityAgentTools.Add(toolsRepository.GetSignature(() => roleAssignmentPluginDefinition.GetRoleDetailsFromNameAsync));

        var graphDBPluginDefinition = new GraphDBPluginDefinition(graphDBPlugin);
        connectivityAgentTools.Add(toolsRepository.GetSignature(() => graphDBPluginDefinition.GetResourceIdForResourceName));

        // Add FunctionAppConnectivityAgent tools to dictionary
        toolSignaturesDictionary[FunctionAppConnectivityAgentKey] = connectivityAgentTools;

        // Create tool signatures for FunctionAppExecutionFailuresAgent
        var executionFailuresAgentTools = new List<string>
        {
            toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait),
            toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete),
            toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser),
            toolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput),
            toolsRepository.GetSignature(() => functionAppsPluginDefinition.ListFunctionAppsAsync),
            toolsRepository.GetSignature(() => armPluginDefinition.GetArmResourceAsJson)
        };

        var githubPluginDefinition = new GitHubIssuePluginDefinition(githubPlugin);
        executionFailuresAgentTools.Add(toolsRepository.GetSignature(() => githubPluginDefinition.CreateGithubIssue));

        var appCodeAnalysisPluginDefinition = new AppCodeAnalysisPluginDefinition(appCodeAnalysisPlugin);
        executionFailuresAgentTools.Add(toolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.WaitInMilliSeconds));

        var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
        executionFailuresAgentTools.Add(toolsRepository.GetSignature(() => metricsPluginDefinition.GetFunctionAppRequestAvailability));

        // Add chart plotting capabilities to execution failures agent
        executionFailuresAgentTools.Add(toolsRepository.GetSignature(() => chartPluginDefinition.PlotTimeSeriesData));

        executionFailuresAgentTools.Add(toolsRepository.GetSignature(() => functionAppExecutionFailuresPluginDefinition.GetFunctionAppExecutionFailures));
        executionFailuresAgentTools.Add(toolsRepository.GetSignature(() => functionAppExecutionFailuresPluginDefinition.GetFunctionAppCallStacks));
        executionFailuresAgentTools.Add(toolsRepository.GetSignature(() => functionAppExecutionFailuresPluginDefinition.GetFailedRequestsPerFunction));
        executionFailuresAgentTools.Add(toolsRepository.GetSignature(() => functionAppExecutionFailuresPluginDefinition.GetHostRuntimeErrorEvents));

        // Add FunctionAppExecutionFailuresAgent tools to dictionary
        toolSignaturesDictionary[FunctionAppExecutionFailuresAgentKey] = executionFailuresAgentTools;

        // Create tool signatures for FunctionAppConfigurationCheckAgent
        var configurationCheckAgentTools = new List<string>
        {
            toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait),
            toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete),
            toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser),
            toolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput),
            toolsRepository.GetSignature(() => armPluginDefinition.GetArmResourceAsJson),
            toolsRepository.GetSignature(() => armPluginDefinition.GetAppSetting),
            toolsRepository.GetSignature(() => armPluginDefinition.UpdateAppSettingsAsync)
        };

        var functionAppConfigurationChecksPluginDefinition = new FunctionAppConfigurationChecksPluginDefinition(functionAppConfigurationChecksPlugin);
        configurationCheckAgentTools.Add(toolsRepository.GetSignature(() => functionAppConfigurationChecksPluginDefinition.GetFunctionAppConfigurationChecks));

        configurationCheckAgentTools.Add(toolsRepository.GetSignature(() => metricsPluginDefinition.GetFunctionAppRequestAvailability));

        // Add chart plotting capabilities to configuration check agent
        configurationCheckAgentTools.Add(toolsRepository.GetSignature(() => chartPluginDefinition.PlotTimeSeriesData));

        // Add FunctionAppConfigurationCheckAgent tools to dictionary
        toolSignaturesDictionary[FunctionAppConfigurationCheckAgentKey] = configurationCheckAgentTools;

        // Set readonly dictionary
        _toolSignatures = toolSignaturesDictionary;
        _durableTaskClient = durableTaskClient;
    }

    public async Task<string> StartOrchestration(
       string functionAppResourceId,
       Guid threadId)
    {
        return await _durableTaskClient.ScheduleNewFunctionAppDiagnosticsAgentInstanceAsync(
            new FunctionAppDiagnosticsAgentInput(
                FunctionAppResourceId: functionAppResourceId,
                ToolSignatures: _toolSignatures,
                ThreadId: threadId),
            new StartOrchestrationOptions(InstanceId: $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}"));
    }

    public static FunctionAppDiagnosticsAgentInput DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<FunctionAppDiagnosticsAgentInput>(serializedOrchestrationInput).ThrowIfNull();
    }
}
