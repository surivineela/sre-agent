// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core;
using Agent.Runtime.MetaAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppJobsAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.RevisionAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.FirstPartySubAgentPlugins.ACA;

[WorkflowClass]
public class ContainerAppJobsAgentPlugin 
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly ContainerAppJobsAgentFactory _jobsAgentFactory;

    public Guid? ThreadId { get; set; }

    public ContainerAppJobsAgentPlugin(
           DurableTaskClient durableTaskClient,
           ContainerAppJobsAgentFactory jobsAgentFactory,
           ILogger<ContainerAppJobsAgent> logger)
           
    {
        _durableTaskClient = durableTaskClient;
        _jobsAgentFactory = jobsAgentFactory;
    }

    [KernelFunction("list_containerapp_jobs_issue_workflows")]
    [WorkflowFunction]
    [Description("List the information of started workflows for investigating container app jobs issue")]
    public async Task<IReadOnlyList<WorkflowMetadata<ContainerAppJobsAgentActivityInput>>> ListContainerAppJobsAgentWorkflows()
    {
        var list = new List<WorkflowMetadata<ContainerAppJobsAgentActivityInput>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            var input = _jobsAgentFactory.DeserializeInput(instance.SerializedInput.ThrowIfNull());
            list.Add(new WorkflowMetadata<ContainerAppJobsAgentActivityInput>(
                WorkflowInstanceId: instance.InstanceId,
                Input: input));
        }
        return list;
    }


    [WorkflowFunction]
    [KernelFunction("start_jobs_workflow")]
    [Description("Start the workflow to investigate jobs issues")]
    public async Task<string> StartJobsWorkflow(
        [Description("Input for Container Apps Job Agent")] ContainerAppJobsAgentActivityInput jobsAgentActivityInput)
    {
        if (ThreadId == null)
        {
            throw new InvalidOperationException("ThreadId must be set before start orchestration.");
        }

        var instanceId = await _jobsAgentFactory.StartOrchestration(jobsAgentActivityInput, ThreadId.Value);
        return $"A workflow has been started to investigate jobs issues, the workflow instance id is: {instanceId}";
    }
}

