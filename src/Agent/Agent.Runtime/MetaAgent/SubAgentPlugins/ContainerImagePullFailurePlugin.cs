using System.ComponentModel;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents.ContainerImagePullFailureAgent;
using Agent.Core;

namespace Agent.Runtime.MetaAgent;
public class ContainerImagePullFailurePlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly ILogger<ContainerImagePullFailurePlugin> _logger;
    private readonly ContainerImagePullFailureAgentFactory _imagePullFailureAgentFactory;

    public ThreadContext? Context { get; set; }

    public ContainerImagePullFailurePlugin(
        DurableTaskClient durableTaskClient,
        ContainerImagePullFailureAgentFactory imagePullFailureAgentFactory,
        ILogger<ContainerImagePullFailurePlugin> logger)
    {
        _durableTaskClient = durableTaskClient;
        _logger = logger;
        _imagePullFailureAgentFactory = imagePullFailureAgentFactory;
    }

    [KernelFunction("list_container_image_pull_workflows")]
    [Description("List the information of started workflows for Container image pull failure investigation")]
    public async Task<IReadOnlyList<WorkflowMetadata<ContainerImagePullFailureInput>>> ListContainerImagePullWorkflows()
    {
        var list = new List<WorkflowMetadata<ContainerImagePullFailureInput>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                InstanceIdPrefix: ContainerImagePullFailureAgentFactory.OrchestrationInstanceIdPrefix,
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            var input = _imagePullFailureAgentFactory.DeserializeInput(instance.SerializedInput.ThrowIfNull());
            list.Add(new WorkflowMetadata<ContainerImagePullFailureInput>(
                WorkflowInstanceId: instance.InstanceId,
                Input: input));
        }

        return list;
    }

    [KernelFunction("start_container_image_pull_investigation")]
    [Description("Start the workflow to investigate and remediate image pull failures specifically in Linux App/Container Apps")]
    public async Task<string> StartContainerImagePullAgent(
        [Description("Detailed description of the image pull issue with information about the Linux/Container App resource.")]
        string message,

        [Description("Resource ID of the affected Linux/Container App.")]
        string resourceId,

        [Description("Docker image reference that failed to pull (e.g., 'myregistry.azurecr.io/myapp:tag').")]
        string imageReference,

        [Description("Error message from the container logs related to the image pull failure.")]
        string errorMessage)
    {
        if (Context == null)
        {
            throw new InvalidOperationException("ThreadContext must be set before start orchestration.");
        }
        var instanceId = await _imagePullFailureAgentFactory.StartOrchestration(
            message,
            resourceId,
            imageReference,
            errorMessage,
            Context);

        return $"A specialized Linux App/Container Apps image pull investigation workflow has been started to diagnose and fix your image pull failures. The workflow instance ID is: {instanceId}";
    }
}
