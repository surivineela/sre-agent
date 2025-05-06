// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Runtime.SubAgents.ServiceBusAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Agent.Runtime.MetaAgent;

public class ServiceBusPlugin
: SimpleResourceSubAgentPluginBase<ServiceBusAgentFactory, ServiceBusAgent, ServiceBusAgentInput, ServiceBusAgentActivity, ServiceBusAgentActivityInput>, IMetaAgentServiceBusPlugin
{
    public ServiceBusPlugin(
        DurableTaskClient durableTaskClient,
        ServiceBusAgentFactory factory,
        ILogger<ServiceBusAgent> logger)
        : base(durableTaskClient, factory, logger)
    {
    }

    [KernelFunction("list_servicebus_workflows")]
    [Description("List the information of started workflows for event hub remediation")]
    public override Task<IReadOnlyList<WorkflowMetadata<ServiceBusAgentActivityInput>>> ListWorkflowsAsync()
    {
        return this.ListWorkflowsImplAsync();
    }

    [KernelFunction("start_servicebus_workflow")]
    [Description("Start the workflow to apply changes to event hubs")]
    public override Task<string> StartAgentAsync(ServiceBusAgentActivityInput input)
    {
        return StartAgentImplAsync(input);
    }
}

