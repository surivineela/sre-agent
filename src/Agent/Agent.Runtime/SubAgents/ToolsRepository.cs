// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Agent.Runtime.SubAgents;

// [Export]
public sealed class ToolsRepository : IMcpConnectable
{
    private readonly Dictionary<string, IToolFunction> _aiFunctions = new();
    private ConcurrentDictionary<McpConnection, IReadOnlyList<string>> _connectionToToolSignatures = new();
    private IServiceProvider _serviceProvider;

    /// <summary>
    /// Returns a chat message for each connected server with instructions on how to use the tools being exposed.
    /// </summary>
    public IEnumerable<ChatMessage> MCPServerInstructions => _connectionToToolSignatures.Keys.Select(c => new ChatMessage(ChatRole.User, c.ServerInstructions));

    public ToolsRepository(IServiceProvider sp)
    {
        _serviceProvider = sp;

        RegisterPlugin<MetricsPluginDefinition>();
        RegisterPlugin<ChartPluginDefinition>();
        RegisterPlugin<RecordActionsPluginDefinition>();

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
        Register200<GraphDBPluginDefinition>(x => x.GetResourceBasicProperties);

        // Not all tools were registered, so registering individually
        Register200<ArmPluginDefinition>(x => x.SetMinimumTlsVersion);
        Register200<ArmPluginDefinition>(x => x.GetTlsSettings);
        Register200<ArmPluginDefinition>(x => x.GetArmResourceAsJson);
        Register200<ArmPluginDefinition>(x => x.PowerOnVirtualMachine);
        Register200<ArmPluginDefinition>(x => x.GetVirtualMachineBootDiagnostics);
        Register200<ArmPluginDefinition>(x => x.CheckConnectivity);
        Register200<ArmPluginDefinition>(x => x.CheckTcpConnectivity);

        RegisterPlugin<TimePluginDefinition>();
        RegisterPlugin<MIConfigurationCheckPluginDefinition>();
        RegisterPlugin<GithubWorkflowTriggerPluginDefinition>();
        RegisterPlugin<RemediationPluginDefinition>();
        RegisterPlugin<AppIdentityUpdatePluginDefinition>();
        RegisterPlugin<ControlFlowPluginDefinition>();
        RegisterPlugin<ApprovalPluginDefinition>();
        RegisterPlugin<ContainerAppPluginDefinition>();
        RegisterPlugin<ReliabilityPluginDefinition>();
        RegisterPlugin<KubePluginDefinition>();

        // Not all tools were registered, so registering individually
        Register200<GitHubIssuePluginDefinition>(x => x.FetchGithubSecurityDependabotAlerts);

        RegisterPlugin<AzureSupportCenterPluginDefinition>();
        RegisterPlugin<ContainerImagePullFailurePluginDefinition>();
    }

    public void RegisterPlugin<T>()
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
            var executeMethodName = submitMethodInfo.GetCustomAttribute<Plugins.Attributes.Submit202Attribute>().ExecuteMethodName;
            var executeMethodInfo = pluginType.GetMethod(executeMethodName, flags);
            Register202<T>(submitMethodInfo, executeMethodInfo);
        }

    }

    public string Register202<T>(
        Expression<Func<T, Delegate>> submitFunctionSelector,
        Expression<Func<T, Delegate>> executeFunctionSelector)
    {
        var submitMethodInfo = AgentToolsRegistry.GetMethodFromExpression(submitFunctionSelector);
        var executeMethodInfo = AgentToolsRegistry.GetMethodFromExpression(executeFunctionSelector);

        return Register202<T>(submitMethodInfo, executeMethodInfo);
    }

    public string Register202<T>(MethodInfo submitMethodInfo, MethodInfo executeMethodInfo)
    {
        var sig = GetSignature(submitMethodInfo);

        _aiFunctions.Add(sig, new DeferredToolFunction202<T>(_serviceProvider, submitMethodInfo, executeMethodInfo));
        return sig;
    }

    public string Register200<T>(Expression<Func<T, Delegate>> executeFunctionSelector)
    {
        var methodInfo = AgentToolsRegistry.GetMethodFromExpression(executeFunctionSelector);
        return Register200<T>(methodInfo);
    }

    public string Register200<T>(MethodInfo methodInfo)
    {
        var sig = GetSignature(methodInfo);

        _aiFunctions.Add(sig, new DeferredToolFunction200<T>(_serviceProvider, methodInfo));
        return sig;
    }

    public List<AITool> ResolveTools(IReadOnlyList<string> toolSignatures)
    {
        return this.GetAllTools(toolSignatures).Select<string, AITool>(sig => this.FindAiFunction(sig).ToolFunction).ToList();
    }

    public static string GetSignature(
        Expression<Func<Delegate>> actionSelector)
    {
        var actionMethod = GetMethod(actionSelector);
        var sig = GetSignature(actionMethod);
        return sig;
    }

    public IToolFunction FindAiFunction(
        string signature)
    {
        // This will throw if entry not found
        return _aiFunctions[signature];
    }

    public Dictionary<string, IToolFunction> GetAllFunctions() => _aiFunctions;

    public static string GetSignature(MethodInfo method)
    {
        if (method.DeclaringType is null
            || method.DeclaringType.FullName is null)
        {
            throw new ArgumentNullException("method's DeclaringType.FullName not exist");
        }

        var className = method.DeclaringType.FullName;
        var methodName = method.Name;
        var parameters = string.Join(", ", method.GetParameters()
            .Select(p => p.ParameterType.FullName));

        return $"{className}.{methodName}({parameters})";
    }

    private static MethodInfo GetMethod<R>(Expression<Func<R>> selector)
        where R : Delegate
    {
        if (selector.Body is UnaryExpression unaryExpression
            && unaryExpression.Operand is MethodCallExpression methodCallExpression
            && methodCallExpression.Object is ConstantExpression constantExpression
            && constantExpression.Value is MethodInfo methodInfo)
        {
            return methodInfo;
        }

        throw new ArgumentOutOfRangeException(nameof(selector));
    }

    private string GetAIFunctionSignature(
        McpConnection connection,
        AITool tool)
    {
        return $"{connection} {tool}";
    }

    /// <inheritdoc />
    public void TryAddServer(McpConnection connection)
    {
        List<string> toolSignatures = [];

        foreach (AIFunction tool in connection.Tools)
        {
            string sig = GetAIFunctionSignature(connection, tool);
            toolSignatures.Add(sig);
            _aiFunctions.TryAdd(sig, new ToolFunction200(tool));
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
}
