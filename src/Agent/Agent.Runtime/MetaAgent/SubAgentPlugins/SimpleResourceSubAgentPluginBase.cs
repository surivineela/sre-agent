using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.MetaAgent;

/// <summary>
/// A base class for all sub-agent plugins. Knows how to list and start agent workflows.
/// </summary>
/// <remarks>Deriving from this ensures that your agent is made available to the MetaAgent,
/// but be sure to update the MetaAgent prompt to tell it about your agent's capabilities.</remarks>
public abstract class SimpleResourceSubAgentPluginBase<TAgentFactory, TAgentType, TAgentInput, TActivity, TActivityInput>
    where TAgentFactory : SimpleResourceSubAgentFactoryBase<TAgentType, TAgentInput, TActivity, TActivityInput>
    where TAgentType : SimpleResourceSubAgentBase<TAgentInput, TActivity, TActivityInput>
    where TAgentInput : SimpleResourceSubAgentInput<TActivityInput>, new()
    where TActivity : SimpleResourceSubAgentActivityBase<TActivityInput>
    where TActivityInput : SimpleResourceSubAgentActivityInput, new()
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly TAgentFactory _agentFactory;
    private readonly ILogger<TAgentType> _logger;
    public Guid? ThreadId { get; set; }

    public SimpleResourceSubAgentPluginBase(
        DurableTaskClient durableTaskClient,
        TAgentFactory factory,
        ILogger<TAgentType> logger)
    {
        _durableTaskClient = durableTaskClient;
        _agentFactory = factory;
        _logger = logger;
    }

    // NOTE: The only reason we force deriving classes to implement the two abstract methods is to
    // allow them to put [Description] attrs on them. However, we could instead choose to auto-gen
    // those descriptions based on the TAgentType name, and pass those directly to AIFunctionFactory.Create
    // inside MetaAgent.cs. If we feel that's sufficient, then we can simplify this class.

    /// <summary>
    /// Override this method and have it just call <see cref="ListWorkflowsImplAsync"/>.
    /// However, be sure to tag this method with two attributes: [Description] and [KernelFunction].
    /// </summary>
    public abstract Task<IReadOnlyList<WorkflowMetadata<TActivityInput>>> ListWorkflowsAsync();

    protected async Task<IReadOnlyList<WorkflowMetadata<TActivityInput>>> ListWorkflowsImplAsync()
    {
        var list = new List<WorkflowMetadata<TActivityInput>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                InstanceIdPrefix: _agentFactory.OrchestrationInstanceIdPrefix,
                Statuses: [],
                FetchInputsAndOutputs: true)))
        {
            // workaround because above fetch inputs and outputs is not working
            var instanceWithInput = await _durableTaskClient.GetInstanceAsync(instance.InstanceId, getInputsAndOutputs: true);
            var agentInput = instanceWithInput.ReadInputAs<TAgentInput>();

            list.Add(new WorkflowMetadata<TActivityInput>(
                WorkflowInstanceId: instance.InstanceId,
                Input: agentInput.ActivityInput));
        }

        return list;
    }

    /// <summary>
    /// Override this method and have it just call <see cref="StartAgentImplAsync(TActivityInput)"/>.
    /// However, be sure to tag this method with two attributes: [Description] and [KernelFunction].
    /// </summary>
    public abstract Task<string> StartAgentAsync(TActivityInput input);

    protected async Task<string> StartAgentImplAsync(TActivityInput input)
    {
        var agentName = typeof(TAgentType).Name;
        if (ThreadId == null)
        {
            throw new InvalidOperationException("ThreadContext must be set before starting orchestration.");
        }

        var instanceId = await _agentFactory.StartOrchestration(input, ThreadId.Value);
        return $"A workflow has been started to for the {agentName} agent, the workflow instance id is: {instanceId}";
    }
}
