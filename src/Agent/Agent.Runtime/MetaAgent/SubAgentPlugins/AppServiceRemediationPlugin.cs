using System.ComponentModel;
using Agent.Runtime.SubAgents.AppServiceRemediation;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Agent.Runtime.MetaAgent;

public class AppServiceRemediationPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly AppServiceRemediationAgentFactory _appServiceRemediationAgentFactory;
    private readonly ILogger<AppServiceRemediationPlugin> _logger;

    public string? ThreadId { get; set; }

    public AppServiceRemediationPlugin(
        DurableTaskClient durableTaskClient,
        AppServiceRemediationAgentFactory appServiceRemediationAgentFactory,
        ILogger<AppServiceRemediationPlugin> logger)
    {
        _durableTaskClient = durableTaskClient;
        _appServiceRemediationAgentFactory = appServiceRemediationAgentFactory;
        _logger = logger;
    }

    [KernelFunction("list_app_service_remediation_workflow")]
    [Description("List the information of started workflow for app service/function app remediation")]
    public async Task<IReadOnlyList<WorkflowMetadata<AppServiceRemediationInput>>> ListAppServiceRemediationWorkflows()
    {
        var list = new List<WorkflowMetadata<AppServiceRemediationInput>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                InstanceIdPrefix: AppServiceRemediationAgentFactory.OrchestrationInstanceIdPrefix,
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            // workaround because above fetch inputs and outputs is not working
            var instanceWithInput = await _durableTaskClient.GetInstanceAsync(instance.InstanceId, getInputsAndOutputs: true);
            var agentInput = instanceWithInput.ReadInputAs<AppServiceRemediationAgentInput>();

            list.Add(new WorkflowMetadata<AppServiceRemediationInput>(
                WorkflowInstanceId: instance.InstanceId,
                Input: agentInput.Input));
        }

        return list;
    }

    [KernelFunction("start_app_service_remediation_workflow")]
    [Description("Start the workflow to remediate azure app service apps or azure function apps for memory leak, network issues, app issues etc.")]
    public async Task<string> StartAppServiceRemediationAgent(
        [Description("The list of complete Azure Resource Id of the app service apps or function apps")] AppServiceRemediationInput input,
        string threadId)
    {

        var instanceId = await _appServiceRemediationAgentFactory.StartOrchestration(input, ThreadId);
        return $"A workflow has been started to remediate app service, the workflow instance id is: {instanceId}";
    }
}
