// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents.StorageAccountAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace Agent.Runtime.MetaAgent;

public class StorageAccountPlugin : SimpleResourceSubAgentPluginBase<StorageAccountAgentFactory, StorageAccountAgent, StorageAccountAgentInput, StorageAccountAgentActivity, StorageAccountAgentActivityInput>,
    IMetaAgentStorageAccountPlugin
{
    public StorageAccountPlugin(
        DurableTaskClient durableTaskClient,
        StorageAccountAgentFactory factory,
        ILogger<StorageAccountAgent> logger)
        : base(durableTaskClient, factory, logger)
    {
    }

    [KernelFunction("list_storage_account_workflows")]
    [Description("List the information of started workflows for storage account remediation")]
    public override Task<IReadOnlyList<WorkflowMetadata<StorageAccountAgentActivityInput>>> ListWorkflowsAsync()
    {
        return this.ListWorkflowsImplAsync();
    }

    [KernelFunction("start_storage_account_workflow")]
    [Description("Start the workflow to apply changes to storage accounts")]
    public override Task<string> StartAgentAsync(StorageAccountAgentActivityInput input)
    {
        return StartAgentImplAsync(input);
    }
}

