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

namespace Agent.Runtime.SubAgents;

// [Export]
public sealed class ToolsRepository
{
    private readonly Dictionary<string, IToolFunction> _aiFunctions = new();

    public ToolsRepository(
        IMetricsPlugin metricsPlugin,
        IArmPlugin armPlugin,
        IApprovalPlugin approvalPlugin,
        ITimePlugin timePlugin,
        IMIConfigurationCheckPlugin miMigrationPlugin,
        IGithubWorkflowTriggerPlugin githubWorkflowTriggerPlugin,
        IAppIdentityUpdatePlugin appIdentityUpdatePlugin)
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

        var armPluginDefinition = new ArmPluginDefinition(armPlugin);
        Register200(() => armPluginDefinition.SetMinimumTlsVersion);
        Register200(() => armPluginDefinition.RestartWebApp);

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
}
