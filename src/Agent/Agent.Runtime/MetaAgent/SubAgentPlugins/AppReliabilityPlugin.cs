using Agent.Core;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents.AppReliabilityAgent;
using Microsoft.DurableTask.Client;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Agent.Runtime.MetaAgent;

public class AppReliabilityPlugin : IMetaAgentAppReliabilityPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly AppReliabilityAgentFactory _appReliabilityAgentFactory;

    public ThreadContext? Context { get; set; }

    public AppReliabilityPlugin(
        DurableTaskClient durableTaskClient,
        AppReliabilityAgentFactory appReliabilityAgentFactory)
    {
        _durableTaskClient = durableTaskClient;
        _appReliabilityAgentFactory = appReliabilityAgentFactory;
    }

    [KernelFunction("list_app_reliability_practice_workflow")]
    [Description("List the information of started app reliability workflow")]
    public async Task<IReadOnlyList<WorkflowMetadata<AppReliabilityInput>>> ListAppReliabilityWorkflows()
    {
        var list = new List<WorkflowMetadata<AppReliabilityInput>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            var input = _appReliabilityAgentFactory.DeserializeInput(instance.SerializedInput.ThrowIfNull());
            list.Add(new WorkflowMetadata<AppReliabilityInput>(
                WorkflowInstanceId: instance.InstanceId,
                Input: input));
        }

        return list;
    }

    [KernelFunction("start_app_reliability_workflow")]
    [Description("Start the workflow to modify multiple apps to adopt best reliability practices.")]
    public async Task<string> StartAppReliabilityAgent(
        [Description("The list of apps to be modified")] AppReliabilityInput input,
        ThreadContext context)
    {
        var instanceId = await _appReliabilityAgentFactory.StartOrchestration(input, context);
        return $"A workflow has been started to adopt best reliability practice, the workflow instance id is: {instanceId}";
    }
}

