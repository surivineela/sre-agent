using Agent.Core;
using Agent.Core.Helpers;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents.WebAppDownAgent;
using Microsoft.DurableTask.Client;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Runtime.MetaAgent;

public class WebAppDownPlugin : IMetaAgentWebAppDownPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly WebAppDownAgentFactory _webAppDownAgentFactory;
    private readonly ArmHelper _armHelper;

    public ThreadContext? Context { get; set; }

    public WebAppDownPlugin(
        DurableTaskClient durableTaskClient,
        WebAppDownAgentFactory webAppDownAgentFactory,
        ArmHelper armHelper)
    {
        _durableTaskClient = durableTaskClient;
        _webAppDownAgentFactory = webAppDownAgentFactory;
        _armHelper = armHelper;
    }

    [KernelFunction("list_web_app_down_workflow")]
    [Description("List the information of started web app down workflow")]
    public async Task<IReadOnlyList<WorkflowMetadata<WebAppDownInput>>> ListWebAppDownWorkflows()
    {
        var list = new List<WorkflowMetadata<WebAppDownInput>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            var input = _webAppDownAgentFactory.DeserializeInput(instance.SerializedInput.ThrowIfNull());
            list.Add(new WorkflowMetadata<WebAppDownInput>(
                WorkflowInstanceId: instance.InstanceId,
                Input: input));
        }

        return list;
    }

    [KernelFunction("start_web_app_down_workflow")]
    [Description("Start the workflow to mitigate and resolve the web apps that are down or slow")]
    public async Task<string> StartWebAppDownAgent(
        [Description("The list of apps to be modified")] WebAppDownInput input,
        ThreadContext context)
    {
        var instanceId = await _webAppDownAgentFactory.StartOrchestration(input, context);
        return $"A workflow has been started to fix the apps facing slowness or downtime, the workflow instance id is: {instanceId}";
    }

    // Tools to implement
    /*
     * 
         -uses Arm API to get custom activity logs, specifically site management operations (e.g. swap operations) 

        -tool to query Application Insights 

        -use ARM API to run AppLens detectors 

        -can trigger a deployment swap (for easy mitigation, if user wants)

     */
}

