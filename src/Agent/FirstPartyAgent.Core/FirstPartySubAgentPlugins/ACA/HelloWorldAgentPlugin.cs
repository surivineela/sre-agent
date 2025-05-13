// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core;
using Agent.Runtime.MetaAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.HelloWorldAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.FirstPartySubAgentPlugins.ACA;

[WorkflowClass]
public class HelloWorldAgentPlugin 
{
    private readonly DurableTaskClient _durableTaskClient;
    public Guid? ThreadId { get; set; }
    private readonly HelloWorldAgentFactory _helloWorldAgentFactory;
    public HelloWorldAgentPlugin(
        DurableTaskClient durableTaskClient,
        HelloWorldAgentFactory factory,
        ILogger<HelloWorldAgent> logger)
        
    {
        _helloWorldAgentFactory = factory;
        _durableTaskClient = durableTaskClient;
    }
    [WorkflowFunction]
    // There MUST be always these two Kernel functions in the plugin for MetaAgent to call this 'HelloWorldAgent' sub-agent.
    // Note: KernelFunctions required for implementing 'HelloWorldAgent' sub-agent tool capabilities MUST be defined inside <reference>FirstPartyAgent.Core.Plugins.Implementation.HelloWorldPlugin</reference>
    [KernelFunction("list_hello_world_workflows")]
    [Description("List the information of started workflows for hello world resources remediation")]
    public async  Task<IReadOnlyList<WorkflowMetadata<HelloWorldAgentActivityInput>>> ListHelloworledWorkflowsAsync()
    {
        var list = new List<WorkflowMetadata<HelloWorldAgentActivityInput>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            var input = _helloWorldAgentFactory.DeserializeInput(instance.SerializedInput.ThrowIfNull());
            list.Add(new WorkflowMetadata<HelloWorldAgentActivityInput>(
                WorkflowInstanceId: instance.InstanceId,
                Input: input));
        }

        return list;
    }
    [WorkflowFunction]
    [KernelFunction("start_hello_world_workflow")]
    [Description("Start the workflow to apply changes to hello world resource")]
    public  async Task<string> StartHelloworldAgentAsync(HelloWorldAgentActivityInput input)
    {
        if (ThreadId == null)
        {
            throw new InvalidOperationException("ThreadId must be set before start orchestration.");
        }

        var instanceId = await _helloWorldAgentFactory.StartOrchestration(input, ThreadId.Value);
        return $"A workflow has been started to fix the apps facing slowness or downtime, the workflow instance id is: {instanceId}";

    }
}
