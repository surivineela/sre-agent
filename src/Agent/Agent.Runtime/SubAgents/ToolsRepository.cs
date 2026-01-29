// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Plugins.Definitions.Connector;
using Agent.Runtime.Helpers;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models;
using Agent.Runtime.Services.Mcp;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Agent.Runtime.SubAgents;

// [Export]
public class ToolsRepository : IToolsRepository
{
    private readonly Dictionary<string, IToolFunction> _aiFunctions = new();
    private ConcurrentDictionary<McpConnection, IReadOnlyList<string>> _connectionToToolSignatures = new();
    private IServiceProvider _serviceProvider;

    protected ToolsRepository(IServiceProvider sp, bool registerThirdPartyPlugins = false)
    {
        _serviceProvider = sp;
        if (registerThirdPartyPlugins)
        {
            RegisterThirdPartyPlugins();
        }
    }
    public ToolsRepository(IServiceProvider sp) : this(sp, true)
    {
    }

    private void RegisterThirdPartyPlugins()
    {
        RegisterPlugin<MetricsPluginDefinition>();
        RegisterPlugin<AzureMonitorMetricsPluginDefinition>();
        RegisterPlugin<ChartPluginDefinition>();
        RegisterPlugin<RecordActionsPluginDefinition>();
        RegisterPlugin<AzureDevOpsWorkItemPluginDefinition>();
        RegisterPlugin<RepositoryPluginDefintion>();
        RegisterPlugin<DGrepPluginDefinition>();

        // Not all tools were registered, so registering individually
        Register200<GrafanaPluginDefinition>(x => x.ModifyGrafanaDashboard);

        // Not all tools were registered, so registering individually
        Register200<GraphDBPluginDefinition>(x => x.FindAllNetworkConnectedResources);
        Register200<GraphDBPluginDefinition>(x => x.GetApplicationComponentsSummary);
        Register200<GraphDBPluginDefinition>(x => x.VisualizeApplicationComponents);
        Register200<GraphDBPluginDefinition>(x => x.VisualizeAKSMicroserviceTopology);
        Register200<GraphDBPluginDefinition>(x => x.DiscoverApplications);
        Register200<GraphDBPluginDefinition>(x => x.AddSourceCodeNodeToContainerAppNode);
        Register200<GraphDBPluginDefinition>(x => x.GetContainerAppsWithNodesWithoutSourceCodeNodes);
        Register200<GraphDBPluginDefinition>(x => x.GetResourceDetailedProperties);
        Register200<GraphDBPluginDefinition>(x => x.SearchResource);
        Register200<GraphDBPluginDefinition>(x => x.AddIgnoreTagToResource);

        // Not all tools were registered, so registering individually
        Register200<ArmPluginDefinition>(x => x.SetMinimumTlsVersion);
        Register200<ArmPluginDefinition>(x => x.GetTlsSettings);
        Register200<ArmPluginDefinition>(x => x.GetArmResourceAsJson);
        Register200<ArmPluginDefinition>(x => x.PowerOnVirtualMachine);
        Register200<ArmPluginDefinition>(x => x.GetVirtualMachineBootDiagnostics);
        Register200<ArmPluginDefinition>(x => x.CheckConnectivityToAzureWebJobsStorage);
        Register200<ArmPluginDefinition>(x => x.CheckTcpConnectivity);
        Register200<ArmPluginDefinition>(x => x.CheckDnsResolution);
        Register200<ArmPluginDefinition>(x => x.GetAppSetting);
        Register200<ArmPluginDefinition>(x => x.ConfigureAppSettingsForManagedIdentityStorage);
        Register200<ArmPluginDefinition>(x => x.ListKeysAndUpdateAppSettingsAsync);

        RegisterPlugin<TimePluginDefinition>();
        RegisterPlugin<OutlookConnectorPluginDefinition>();
        RegisterPlugin<MIConfigurationCheckPluginDefinition>();
        RegisterPlugin<GithubWorkflowTriggerPluginDefinition>();
        RegisterPlugin<RemediationPluginDefinition>();
        RegisterPlugin<AppIdentityUpdatePluginDefinition>();
        RegisterPlugin<ControlFlowPluginDefinition>();
        RegisterPlugin<NSGRulePluginDefinition>();
        RegisterPlugin<ContainerAppPluginDefinition>();
        RegisterPlugin<ReliabilityPluginDefinition>();
        RegisterPlugin<KubePluginDefinition>();
        RegisterPlugin<AppCodeAnalysisPluginDefinition>();
        RegisterPlugin<CpuAnalysisPluginDefinition>();
        RegisterPlugin<WebAppPluginDefinition>();
        RegisterPlugin<RoleAssignmentPluginDefinition>();
        RegisterPlugin<FunctionAppsPluginDefinition>();
        RegisterPlugin<LogicAppsPluginDefinition>();
        RegisterPlugin<FunctionAppExecutionFailuresPluginDefinition>();
        RegisterPlugin<FunctionAppConfigurationChecksPluginDefinition>();
        RegisterPlugin<FunctionAppDeploymentChecksPluginDefinition>();
        RegisterPlugin<LinuxWebAppRuntimeStatusPluginDefinition>();
        RegisterPlugin<PostgreSQLPluginDefinition>();
        RegisterPlugin<PagerDutyIncidentPluginDefinition>();
        RegisterPlugin<MdmMetricsPluginDefinition>();
        RegisterPlugin<AzureAlertingPluginDefinition>();
        RegisterPlugin<AppInsightsPluginDefinition>();
        RegisterPlugin<CannotConnectToVmPluginDefinition>();
        RegisterPlugin<RedisPluginDefinition>();
        RegisterPlugin<DGrepPluginDefinition>();
        RegisterPlugin<KnowledgeGraphPluginDefinition>();

        // Not all tools were registered, so registering individually
        Register200<GitHubIssuePluginDefinition>(x => x.FetchGithubSecurityDependabotAlerts);
        Register200<GitHubIssuePluginDefinition>(x => x.CreateGithubIssue);
        Register200<GitHubIssuePluginDefinition>(x => x.CreateGithubIssueComment);
        Register200<GitHubIssuePluginDefinition>(x => x.FetchGithubIssue);
        Register200<GitHubIssuePluginDefinition>(x => x.FindConnectedGitHubRepo);
        RegisterPlugin<SourceCodeAnalysisAgentPluginDefinition>();

        RegisterPlugin<AzureSupportCenterPluginDefinition>();
        RegisterPlugin<CodeOptimizationsPluginDefinition>();
    }

    /// <summary>
    /// Returns a chat message for each connected server with instructions on how to use the tools being exposed.
    /// </summary>
    public IEnumerable<ChatMessage> GetMCPServerInstructions()
    {
        return _connectionToToolSignatures.Keys.Select(c => new ChatMessage(ChatRole.User, c.ServerInstructions));
    }

    public void RegisterPlugin<T>() where T : notnull
    {
        var pluginType = typeof(T);
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        // Get all public methods with Description attribute
        var methodsToRegister = pluginType.GetMethods(flags)
            .Where(m => m.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>() != null)
            .ToList();

        var methods200 = methodsToRegister
            .Where(m => m.GetCustomAttribute<Plugins.Attributes.Submit202Attribute>() == null)
            .ToList();

        var methods202 = methodsToRegister
            .Where(m => m.GetCustomAttribute<Plugins.Attributes.Submit202Attribute>() != null)
            .ToList();

        foreach (var methodInfo in methods200)
        {
            Register200<T>(methodInfo);
        }

        foreach (var submitMethodInfo in methods202)
        {
            var executeMethodName = submitMethodInfo.GetCustomAttribute<Plugins.Attributes.Submit202Attribute>()?.ExecuteMethodName;
            if (executeMethodName == null)
            {
                continue;
            }
            var executeMethodInfo = pluginType.GetMethod(executeMethodName, flags);
            if (executeMethodInfo == null)
            {
                continue;
            }
            Register202<T>(submitMethodInfo, executeMethodInfo);
        }

    }

    public string Register202<T>(
        Expression<Func<T, Delegate>> submitFunctionSelector,
        Expression<Func<T, Delegate>> executeFunctionSelector) where T : notnull
    {
        var submitMethodInfo = ToolHelper.GetMethodFromExpression(submitFunctionSelector);
        var executeMethodInfo = ToolHelper.GetMethodFromExpression(executeFunctionSelector);

        return Register202<T>(submitMethodInfo, executeMethodInfo);
    }

    public string Register202<T>(MethodInfo submitMethodInfo, MethodInfo executeMethodInfo) where T : notnull
    {
        var sig = GetSignature(submitMethodInfo);
        _aiFunctions.Add(sig, new DeferredToolFunction202<T>(_serviceProvider, submitMethodInfo, executeMethodInfo));
        return sig;
    }

    public string Register200<T>(Expression<Func<T, Delegate>> executeFunctionSelector) where T : notnull
    {
        var methodInfo = ToolHelper.GetMethodFromExpression(executeFunctionSelector);
        return Register200<T>(methodInfo);
    }

    public string Register200<T>(MethodInfo methodInfo) where T : notnull
    {
        var sig = GetSignature(methodInfo);
        _aiFunctions.Add(sig, new DeferredToolFunction200<T>(_serviceProvider, methodInfo));
        return sig;
    }

    public List<AITool> ResolveTools(IReadOnlyList<string> toolSignatures)
    {
        // Step 1: Retrieve all tools
        var allTools = this.GetAllTools(toolSignatures);

        // Step 2: Map tool signatures to AITool objects
        var aiTools = allTools.Select<string, AITool>(sig =>
        {
            var toolFunction = this.FindAiFunction(sig).ToolFunction;
            return toolFunction;
        }).ToList();

        // Step 3: Return the resolved tools
        return aiTools;
    }

    public string GetSignature(
        Expression<Func<Delegate>> actionSelector)
    {
        return ToolHelper.GetSignature(actionSelector);
    }

    public IToolFunction FindAiFunction(
        string signature)
    {
        // This will throw if entry not found
        return _aiFunctions[signature];
    }

    public Dictionary<string, IToolFunction> GetAllFunctions() => _aiFunctions;

    public string GetSignature(MethodInfo method)
    {
        return ToolHelper.GetSignature(method);
    }

    private string GetMcpToolSignature(
        McpConnection connection,
        AITool tool)
    {
        // IMPORTANT: Always prefix MCP tools with connection ID for unique namespacing
        // This prevents conflicts between different MCP servers with similar tool names
        // Format: {connectionId}_{toolName}
        // Example: "github_mcp_create_issue", "azure_mcp_list_resources"
        return $"{connection.Id}_{tool.Name}";
    }

    /// <inheritdoc />
    public void TryAddServer(McpConnection connection)
    {
        List<string> toolSignatures = [];

        if (connection.Tools != null)
        {
            // Get the health service from DI container
            var healthService = _serviceProvider.GetService<IMcpConnectionHealthService>();

            foreach (AIFunction tool in connection.Tools)
            {
                string sig = GetMcpToolSignature(connection, tool);
                toolSignatures.Add(sig);

                // Wrap the tool with a renamed version that uses the prefixed signature
                // but delegates to the original tool for execution with health checking
                var mcpTool = new McpToolAIFunction(sig, tool, healthService);
                _aiFunctions.TryAdd(sig, new ToolFunction200(mcpTool));
            }
        }

        _connectionToToolSignatures.TryAdd(connection, toolSignatures.AsReadOnly());
    }

    /// <inheritdoc />
    public void TryRemoveServer(McpConnection connection)
    {
        if (_connectionToToolSignatures.TryRemove(connection, out IReadOnlyList<string>? toolSignatures))
        {
            foreach (string sig in toolSignatures)
            {
                _aiFunctions.Remove(sig);
            }
        }
    }

    public IReadOnlyList<string> GetAllTools(IReadOnlyList<string> localTools)
    {
        return _connectionToToolSignatures.Values.SelectMany(t => t).Concat(localTools).ToList().AsReadOnly();
    }

    List<AIFunction> IMcpConnectable.GetAllFunctions()
    {
        return _aiFunctions.Values.Select(t => t.ToolFunction).ToList();
    }

}
