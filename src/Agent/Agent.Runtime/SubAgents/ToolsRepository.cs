using Agent.Plugins.Definitions;
using Agent.Plugins;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models;
using Agent.Core.Helpers;
using System.Collections.Concurrent;
using Agent.Runtime.SubAgents.Core;

namespace Agent.Runtime.SubAgents;

// [Export]
public sealed class ToolsRepository : IMcpConnectable
{
    private readonly Dictionary<string, IToolFunction> _aiFunctions = new();
    private ConcurrentDictionary<McpConnection, IReadOnlyList<string>> _connectionToToolSignatures = new();

    /// <summary>
    /// Returns a chat message for each connected server with instructions on how to use the tools being exposed.
    /// </summary>
    public IEnumerable<ChatMessage> MCPServerInstructions => _connectionToToolSignatures.Keys.Select(c => new ChatMessage(ChatRole.User, c.ServerInstructions));

    public ToolsRepository(
        IMetricsPlugin metricsPlugin,
        IArmPlugin armPlugin,
        IApprovalPlugin approvalPlugin,
        ITimePlugin timePlugin,
        IChartPlugin chartPlugin,
        IMIConfigurationCheckPlugin miMigrationPlugin,
        IGithubWorkflowTriggerPlugin githubWorkflowTriggerPlugin,
        IAppIdentityUpdatePlugin appIdentityUpdatePlugin,
        IRemediationPlugin remediationPlugin,
        IRecordActionsPlugin recordActionsPlugin)
    {
        var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
        Register200(() => metricsPluginDefinition.GetSuccessfulRequestVolumeAsync);
        Register200(() => metricsPluginDefinition.GetWebAppCpuMetrics);
        Register202(
            submitFunctionSelector: () => metricsPluginDefinition.StartGetWebAppCpuMetrics,
            executeFunctionSelector: () => metricsPluginDefinition.GetWebAppCpuMetrics);
        Register200(() => metricsPluginDefinition.GetMemoryMetrics);
        Register202(
            submitFunctionSelector: () => metricsPluginDefinition.StartGetMemoryMetrics,
            executeFunctionSelector: () => metricsPluginDefinition.GetMemoryMetrics);
        Register200(() => metricsPluginDefinition.GetFunctionAppRequestAvailability);

        var chartPluginDefinition = new ChartPluginDefinition(chartPlugin);
        Register200(() => chartPluginDefinition.PlotTimeSeriesDataAsync);
        Register200(() => chartPluginDefinition.PlotPieChartAsync);
        Register200(() => chartPluginDefinition.PlotBarChartAsync);

        var recordActionsPluginDefinition = new RecordActionsPluginDefinition(recordActionsPlugin);
        Register200(() => recordActionsPluginDefinition.RecordAction);
        Register200(() => recordActionsPluginDefinition.GetActionDetails);

        var armPluginDefinition = new ArmPluginDefinition(armPlugin);
        Register200(() => armPluginDefinition.SetMinimumTlsVersion);
        Register200(() => armPluginDefinition.RestartWebApp);
        Register200(() => armPluginDefinition.GetTlsSettings);

        var timePluginDefinition = new TimePluginDefinition(timePlugin);
        Register200(() => timePluginDefinition.GetCurrentUtcTime);
        Register200(() => timePluginDefinition.GetAppTimeZone);

        var miMigrationPluginDefinition = new MIConfigurationCheckPluginDefinition(miMigrationPlugin);
        Register200(() => miMigrationPluginDefinition.CheckSqlConnectionTypeAsync);
        Register200(() => miMigrationPluginDefinition.CheckSqlResourceIdForAppAsync);

        var githubWorkflowTriggerPluginDefinition = new GithubWorkflowTriggerPluginDefinition(githubWorkflowTriggerPlugin);
        Register200(() => githubWorkflowTriggerPluginDefinition.CheckPullRequestMergeStatus);
        Register200(() => githubWorkflowTriggerPluginDefinition.TriggerWorkflow);
        Register200(() => githubWorkflowTriggerPluginDefinition.TrackWorkflow);

        var remediationPluginDefinition = new RemediationPluginDefinition(remediationPlugin);
        Register200(() => remediationPluginDefinition.ScaleAppServicePlanVertically);
        Register200(() => remediationPluginDefinition.SuggestNextSku);
        Register200(() => remediationPluginDefinition.CalculateScalingCost);
        Register200(() => remediationPluginDefinition.RestartWebApp);
        Register200(() => remediationPluginDefinition.CollectMemoryDump);

        var appIdentityUpdatePluginDefinition = new AppIdentityUpdatePluginDefinition(appIdentityUpdatePlugin);
        Register200(() => appIdentityUpdatePluginDefinition.GetAppManagedIdentityAsync);
        Register200(() => appIdentityUpdatePluginDefinition.MigrateWebAppConnStr2ManagedIdentityAsync);
        Register200(() => appIdentityUpdatePluginDefinition.EnableSqlAdEntraAdminAsync);

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        Register200(() => controlFlowPluginDefinition.Wait);
        Register200(() => controlFlowPluginDefinition.MarkPlanComplete);
        Register200(() => controlFlowPluginDefinition.NotifyUser);

        var approvalPluginDefinition = new ApprovalPluginDefinition(approvalPlugin);
        Register200(() => approvalPluginDefinition.StartApprovalFlow);
    }

    public string Register202(
        Expression<Func<Delegate>> submitFunctionSelector,
        Expression<Func<Delegate>> executeFunctionSelector)
    {
        var sig = GetSignature(submitFunctionSelector);
        // This will throw if `sig` already exists, update GetSignature to avoid conflicts
        _aiFunctions.Add(
            sig,
            new ToolFunction202(
                submitFunction: submitFunctionSelector.Compile().Invoke(),
                executeFunction: executeFunctionSelector.Compile().Invoke()));
        return sig;
    }

    public string Register200(
        Expression<Func<Delegate>> executeFunctionSelector)
    {
        var sig = GetSignature(executeFunctionSelector);
        // This will throw if `sig` already exists, update GetSignature to avoid conflicts
        _aiFunctions.Add(sig, new ToolFunction200(executeFunctionSelector.Compile().Invoke()));
        return sig;
    }

    public string GetSignature(
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

    private static string GetSignature(MethodInfo method)
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
