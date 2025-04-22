using Agent.Core.Models;
using Agent.Plugins.Definitions;
using Agent.Plugins;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using Agent.Core;
using Agent.Core.Models.Api.v1;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Agent.Plugins.Implementation;
using Agent.Runtime.MetaAgent;
using Agent.Core.Helpers;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace Agent.Runtime.SubAgents.CPUAnalysisAgent;


// [Export]
public sealed class CPUAnalysisAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;

    public const string OrchestrationInstanceIdPrefix = nameof(CPUAnalysisAgent);

    public CPUAnalysisAgentFactory(
        IMetricsPlugin metricsPlugin,
        IApprovalPlugin approvalPlugin,
        IGithubIssuePlugin githubPlugin,
        ToolsRepository toolsRepository,
        DurableTaskClient durableTaskClient,
        ICpuAnalysisPlugin cpuAnalysisPlugin,
        IAppCodeAnalysisPlugin appCodeAnalysisPlugin)
    {
        var toolSignatures = new List<string>();
        var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => metricsPluginDefinition.GetWebAppCpuMetrics));
        toolSignatures.Add(ToolsRepository.GetSignature(() => metricsPluginDefinition.GetFunctionAppRequestAvailability));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        // var approvalPluginDefinition = new ApprovalPluginDefinition(approvalPlugin);
        // toolSignatures.Add(toolsRepository.GetSignature(() => approvalPluginDefinition.StartApprovalFlow));

        var githubPluginDefinition = new GitHubIssuePluginDefinition(githubPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => githubPluginDefinition.CreateGithubIssue));

        var cpuAnalysisPluginDefinition = new CpuAnalysisPluginDefinition(cpuAnalysisPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => cpuAnalysisPluginDefinition.ScaleUpAppServicePlanBySku));
        toolSignatures.Add(ToolsRepository.GetSignature(() => cpuAnalysisPluginDefinition.CollectMemoryDumpForApp));
        toolSignatures.Add(ToolsRepository.GetSignature(() => cpuAnalysisPluginDefinition.AutoScaleApp));

        var appCodeAnalysisPluginDefinition = new AppCodeAnalysisPluginDefinition(appCodeAnalysisPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.WaitInMilliSeconds));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
    }

    public async Task<string> StartOrchestration(
        CPUAnalysisInput input,
        Guid threadId)
    {
        return await _durableTaskClient.ScheduleNewCPUAnalysisAgentInstanceAsync(
            new CPUAnalysisAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                ThreadId: threadId),
            new StartOrchestrationOptions(InstanceId: $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}"));
    }

    public CPUAnalysisInput DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<CPUAnalysisAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
    }

}
