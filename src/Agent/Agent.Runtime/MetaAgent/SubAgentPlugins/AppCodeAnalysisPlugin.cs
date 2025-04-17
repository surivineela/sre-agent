using Agent.Core;
using Agent.Core.Helpers;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Plugins;
using Agent.Runtime.SubAgents.AppCodeAnalysisAgent;
using Agent.Runtime.SubAgents.CPUAnalysisAgent;
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

public class AppCodeAnalysisPlugin : IMetaAgentAppCodeAnalysisPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly AppCodeAnalysisAgentFactory _appCodeAnalysisAgentFactory;

    public ThreadContext? Context { get; set; }

    public AppCodeAnalysisPlugin(
        DurableTaskClient durableTaskClient,
        AppCodeAnalysisAgentFactory appCodeAnalysisFactory)
    {
        _durableTaskClient = durableTaskClient;
        _appCodeAnalysisAgentFactory = appCodeAnalysisFactory;

    }


    [KernelFunction("list_app_code_analysis_practice_workflow")]
    [Description("List the information of started app code analysis workflow")]
    public async Task<IReadOnlyList<WorkflowMetadata<AppCodeAnalysisInput>>> ListAppCodeAnalysisWorkflows()
    {
        var list = new List<WorkflowMetadata<AppCodeAnalysisInput>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            var input = _appCodeAnalysisAgentFactory.DeserializeInput(instance.SerializedInput.ThrowIfNull());
            list.Add(new WorkflowMetadata<AppCodeAnalysisInput>(
                WorkflowInstanceId: instance.InstanceId,
                Input: input));
        }

        return list;
    }

    [KernelFunction("start_app_code_analysis_workflow")]
    [Description("Start the workflow to resolve multiple apps with app code issues")]
    public async Task<string> StartAppCodeAnalysisAgent(
        [Description("The list of apps to be modified")] AppCodeAnalysisInput input,
        ThreadContext context)
    {
        var instanceId = await _appCodeAnalysisAgentFactory.StartOrchestration(input, context);
        return $"A workflow has been started to fix the apps' code issues, the workflow instance id is: {instanceId}";
    }

}

