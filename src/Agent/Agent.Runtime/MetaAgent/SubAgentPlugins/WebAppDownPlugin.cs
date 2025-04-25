using Agent.Core;
using Agent.Core.Helpers;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents.WebAppDownAgent;
using Microsoft.DurableTask.Client;
using Microsoft.SemanticKernel;
using System.ComponentModel;


namespace Agent.Runtime.MetaAgent;

public class WebAppDownPlugin : IMetaAgentWebAppDownPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly WebAppDownAgentFactory _webAppDownAgentFactory;

    public Guid? ThreadId { get; set; }

    public WebAppDownPlugin(
        DurableTaskClient durableTaskClient,
        WebAppDownAgentFactory webAppDownAgentFactory)
    {
        _durableTaskClient = durableTaskClient;
        _webAppDownAgentFactory = webAppDownAgentFactory;
    }

    [KernelFunction("list_web_app_down_workflow")]
    [Description("List the information of started web app down workflow")]
    public async Task<IReadOnlyList<WorkflowMetadata<string>>> ListWebAppDownWorkflows()
    {
        var list = new List<WorkflowMetadata<string>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            var input = _webAppDownAgentFactory.DeserializeInput(instance.SerializedInput.ThrowIfNull());
            list.Add(new WorkflowMetadata<string>(
                WorkflowInstanceId: instance.InstanceId,
                Input: input));
        }

        return list;
    }

    [KernelFunction("start_web_app_down_workflow")]
    [Description("Start the workflow to mitigate and resolve the web app that is down")]
    public async Task<string> StartWebAppDownAgent(
        [Description("the resource id of the app service resource to be fixed")] string resourceId)
    {
        if (ThreadId == null)
        {
            throw new InvalidOperationException("ThreadId must be set before start orchestration.");
        }

        var instanceId = await _webAppDownAgentFactory.StartOrchestration(resourceId, ThreadId.Value);
        return $"A workflow has been started to fix the apps facing slowness or downtime, the workflow instance id is: {instanceId}";
    }
}
