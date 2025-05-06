// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Runtime.SubAgents.AzureSqlServerAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Agent.Runtime.MetaAgent;

public class AzureSqlServerPlugin
: SimpleResourceSubAgentPluginBase<AzureSqlServerAgentFactory, AzureSqlServerAgent, AzureSqlServerAgentInput, AzureSqlServerActivity, AzureSqlServerAgentActivityInput>,
    IMetaAgentAzureSqlDbPlugin
{
    public AzureSqlServerPlugin(
        DurableTaskClient durableTaskClient,
        AzureSqlServerAgentFactory factory,
        ILogger<AzureSqlServerAgent> logger)
        : base(durableTaskClient, factory, logger)
    {
    }

    [KernelFunction("list_azure_sql_server_workflows")]
    [Description("List the information of started workflows for AzureSqlServer remediation")]
    public override Task<IReadOnlyList<WorkflowMetadata<AzureSqlServerAgentActivityInput>>> ListWorkflowsAsync()
    {
        return this.ListWorkflowsImplAsync();
    }

    [KernelFunction("start_azure_sql_server_workflow")]
    [Description("Start the workflow to apply changes to AzureSqlServer's")]
    public override Task<string> StartAgentAsync(AzureSqlServerAgentActivityInput input)
    {
        return StartAgentImplAsync(input);
    }
}

