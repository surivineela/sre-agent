// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Runtime.SubAgents.CosmosDbAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Agent.Runtime.MetaAgent;

public class CosmosDbPlugin
: SimpleResourceSubAgentPluginBase<CosmosDbAgentFactory, CosmosDbAgent, CosmosDbAgentInput, CosmosDbAgentActivity, CosmosDbAgentActivityInput>
{
    public CosmosDbPlugin(
        DurableTaskClient durableTaskClient,
        CosmosDbAgentFactory factory,
        ILogger<CosmosDbAgent> logger)
        : base(durableTaskClient, factory, logger)
    {
    }

    [KernelFunction("list_cosmosDb_workflows")]
    [Description("List the information of started workflows for cosmosDb remediation")]
    public override Task<IReadOnlyList<WorkflowMetadata<CosmosDbAgentActivityInput>>> ListWorkflowsAsync()
    {
        return this.ListWorkflowsImplAsync();
    }

    [KernelFunction("start_cosmosDb_workflow")]
    [Description("Start the workflow to apply changes to cosmosDb's")]
    public override Task<string> StartAgentAsync(CosmosDbAgentActivityInput input)
    {
        return StartAgentImplAsync(input);
    }
}

