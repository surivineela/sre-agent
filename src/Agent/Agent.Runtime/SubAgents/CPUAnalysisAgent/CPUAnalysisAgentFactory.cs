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
using Agent.Core.Attributes;

namespace Agent.Runtime.SubAgents.CPUAnalysisAgent;


// [Export]
public sealed class CPUAnalysisAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IToolsRepository _toolsRepository;

    public const string OrchestrationInstanceIdPrefix = nameof(CPUAnalysisAgent);

    public CPUAnalysisAgentFactory(
        IMetricsPlugin metricsPlugin,
        IGithubIssuePlugin githubPlugin,
        IToolsRepository toolsRepository,
        DurableTaskClient durableTaskClient,
        ICpuAnalysisPlugin cpuAnalysisPlugin,
        IAppCodeAnalysisPlugin appCodeAnalysisPlugin,
        IDotnetAnalysisPlugin dotnetAnalysisPlugin)
    {
        _toolsRepository = toolsRepository;
        var toolSignatures = new List<string>();
        var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => metricsPluginDefinition.GetWebAppCpuMetrics));
        toolSignatures.Add(_toolsRepository.GetSignature(() => metricsPluginDefinition.GetFunctionAppRequestAvailability));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        var githubPluginDefinition = new GitHubIssuePluginDefinition(githubPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => githubPluginDefinition.CreateGithubIssue));

        var cpuAnalysisPluginDefinition = new CpuAnalysisPluginDefinition(cpuAnalysisPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => cpuAnalysisPluginDefinition.ScaleUpAppServicePlanBySku));
        toolSignatures.Add(_toolsRepository.GetSignature(() => cpuAnalysisPluginDefinition.AutoScaleApp));

        var appCodeAnalysisPluginDefinition = new AppCodeAnalysisPluginDefinition(appCodeAnalysisPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => appCodeAnalysisPluginDefinition.WaitInMilliSeconds));

        var dotnetAnalysisPluginDefinition = new DotnetAnalysisPluginDefinition(dotnetAnalysisPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => dotnetAnalysisPluginDefinition.GetMemoryAnalysis));
        toolSignatures.Add(_toolsRepository.GetSignature(() => dotnetAnalysisPluginDefinition.ShouldTriggerMemoryDump));
        toolSignatures.Add(_toolsRepository.GetSignature(() => dotnetAnalysisPluginDefinition.GetCPUAnalysis));
        toolSignatures.Add(_toolsRepository.GetSignature(() => dotnetAnalysisPluginDefinition.GetGCCPUAnalysis));

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
