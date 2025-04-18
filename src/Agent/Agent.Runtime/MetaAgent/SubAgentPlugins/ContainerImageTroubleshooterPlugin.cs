using System.ComponentModel;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents.ContainerImagePullFailureAgent;
using Agent.Core;


namespace Agent.Runtime.MetaAgent;

public class ContainerImageTroubleshooterPlugin : IMetaAgentContainerImageTroubleshooterPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly ILogger<ContainerImageTroubleshooterPlugin> _logger;
    private readonly ContainerImagePullFailureAgentFactory _containerImagePullFailureAgentFactory;

    public Guid? ThreadId { get; set; }

    public ContainerImageTroubleshooterPlugin(
        DurableTaskClient durableTaskClient,
        ContainerImagePullFailureAgentFactory imagePullFailureAgentFactory,
        ILogger<ContainerImageTroubleshooterPlugin> logger)
    {
        _durableTaskClient = durableTaskClient;
        _logger = logger;
        _containerImagePullFailureAgentFactory = imagePullFailureAgentFactory;
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
            var input = _containerImagePullFailureAgentFactory.DeserializeInput(instance.SerializedInput.ThrowIfNull());
            list.Add(new WorkflowMetadata<ContainerImagePullFailureInput>(
                WorkflowInstanceId: instance.InstanceId,
                Input: input));
        }

        return list;
    }

    [KernelFunction("start_container_image_pull_investigation")]
    [Description("Start the workflow to investigate and remediate image pull failures specifically in Linux App/Container Apps")]
    public async Task<string> StartContainerImagePullAgent(
        [Description("Resource ID of the affected Linux/Container App.")]
        string resourceId)
    {
        if (ThreadId == null)
        {
            throw new InvalidOperationException("ThreadId must be set before start orchestration.");
        }
        var instanceId = await _containerImagePullFailureAgentFactory.StartOrchestration(
            resourceId,
            ThreadId.Value);

        return $"A specialized Linux App/Container Apps image pull investigation workflow has been started to diagnose and fix your image pull failures. The workflow instance ID is: {instanceId}";
    }
}
