using Agent.Core;
using Agent.Core.Helpers;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents.AppReliabilityAgent;
using Agent.Runtime.SubAgents.CPUAnalysisAgent;
using Microsoft.DurableTask.Client;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Runtime.MetaAgent;

public class CPUAnalysisPlugin : IMetaAgentCPUAnalysisPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly CPUAnalysisAgentFactory _cpuAnalysisAgentFactory;
    private readonly ArmHelper _armHelper;

    public ThreadContext? Context { get; set; }

    public CPUAnalysisPlugin(
        DurableTaskClient durableTaskClient,
        CPUAnalysisAgentFactory cpuAnalysisAgentFactory,
        ArmHelper armHelper)
    {
        _durableTaskClient = durableTaskClient;
        _cpuAnalysisAgentFactory = cpuAnalysisAgentFactory;
        _armHelper = armHelper;
    }

    [KernelFunction("list_cpu_analysis_practice_workflow")]
    [Description("List the information of started cpu analysis workflow")]
    public async Task<IReadOnlyList<WorkflowMetadata<CPUAnalysisInput>>> ListCPUAnalysisWorkflows()
    {
        var list = new List<WorkflowMetadata<CPUAnalysisInput>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            var input = _cpuAnalysisAgentFactory.DeserializeInput(instance.SerializedInput.ThrowIfNull());
            list.Add(new WorkflowMetadata<CPUAnalysisInput>(
                WorkflowInstanceId: instance.InstanceId,
                Input: input));
        }

        return list;
    }

    [KernelFunction("start_cpu_analysis_workflow")]
    [Description("Start the workflow to resolve multiple apps with high CPU.")]
    public async Task<string> StartCPUAnalysisAgent(
        [Description("The list of apps to be modified")] CPUAnalysisInput input,
        ThreadContext context)
    {
        var instanceId = await _cpuAnalysisAgentFactory.StartOrchestration(input, context);
        return $"A workflow has been started to adopt best reliability practice, the workflow instance id is: {instanceId}";
    }
}

