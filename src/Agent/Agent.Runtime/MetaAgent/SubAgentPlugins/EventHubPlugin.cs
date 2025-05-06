// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Runtime.SubAgents.EventHubAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Agent.Runtime.MetaAgent;

public class EventHubPlugin
: SimpleResourceSubAgentPluginBase<EventHubAgentFactory, EventHubAgent, EventHubAgentInput, EventHubAgentActivity, EventHubAgentActivityInput>, IMetaAgentEventHubPlugin
{
    public EventHubPlugin(
        DurableTaskClient durableTaskClient,
        EventHubAgentFactory factory,
        ILogger<EventHubAgent> logger)
        : base(durableTaskClient, factory, logger)
    {
    }

    [KernelFunction("list_eventHub_workflows")]
    [Description("List the information of started workflows for event hub remediation")]
    public override Task<IReadOnlyList<WorkflowMetadata<EventHubAgentActivityInput>>> ListWorkflowsAsync()
    {
        return this.ListWorkflowsImplAsync();
    }

    [KernelFunction("start_eventHub_workflow")]
    [Description("Start the workflow to apply changes to event hubs")]
    public override Task<string> StartAgentAsync(EventHubAgentActivityInput input)
    {
        return StartAgentImplAsync(input);
    }
}

