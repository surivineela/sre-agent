// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Runtime.SubAgents.ContainerAppsRemediation;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Agent.Core.Models.Api.v1;


namespace Agent.Runtime.MetaAgent;

public class ContainerAppsRemediationPlugin : IMetaAgentContainerAppsRemediationPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly ContainerAppsRemediationAgentFactory _containerAppsRemediationAgentFactory;
    private readonly ILogger<ContainerAppsRemediationPlugin> _logger;

    public ThreadContext? Context { get; set; }

    public ContainerAppsRemediationPlugin(
        DurableTaskClient durableTaskClient,
        ContainerAppsRemediationAgentFactory containerAppsRemediationAgentFactory,
        ILogger<ContainerAppsRemediationPlugin> logger)
    {
        _durableTaskClient = durableTaskClient;
        _containerAppsRemediationAgentFactory = containerAppsRemediationAgentFactory;
        _logger = logger;
    }

    [KernelFunction("list_containerapps_remediation_workflow")]
    [Description("List the information of started workflow for azure container apps app remediation")]
    public async Task<IReadOnlyList<WorkflowMetadata<string>>> ListContainerAppsRemediationWorkflows()
    {
        var list = new List<WorkflowMetadata<string>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                InstanceIdPrefix: ContainerAppsRemediationAgentFactory.OrchestrationInstanceIdPrefix,
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            var agentInput = instance.ReadInputAs<ContainerAppsRemediationAgentInput>();

            list.Add(new WorkflowMetadata<string>(
                WorkflowInstanceId: instance.InstanceId,
                Input: agentInput.Input));
        }

        return list;
    }

    [KernelFunction("start_container_apps_remediation_workflow")]
    [Description("Start the workflow to remediate azure container apps for memory leak, network issues, app issues etc")]
    public async Task<string> StartContainerAppsRemediationAgent(
        [Description("The list of complete Azure Resource Id of the apps having the issue and a description of the problem")] string input)
    {
        if (Context == null)
        {
            throw new InvalidOperationException("ThreadContext must be set before start orchestration.");
        }
        var instanceId = await _containerAppsRemediationAgentFactory.StartOrchestration(input, Context);
        return $"A workflow has been started to remediate container apps, the workflow instance id is: {instanceId}";
    }
}

