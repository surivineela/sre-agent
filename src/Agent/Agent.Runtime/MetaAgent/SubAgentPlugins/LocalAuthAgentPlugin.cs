// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Runtime.SubAgents.LocalAuthAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Agent.Runtime.MetaAgent;

public class LocalAuthAgentPlugin
: SimpleResourceSubAgentPluginBase<LocalAuthAgentFactory, LocalAuthAgent, LocalAuthAgentInput, LocalAuthAgentActivity, LocalAuthAgentActivityInput>, IMetaAgentLocalAuthPlugin
{
    public LocalAuthAgentPlugin(
        DurableTaskClient durableTaskClient,
        LocalAuthAgentFactory factory,
        ILogger<LocalAuthAgent> logger)
        : base(durableTaskClient, factory, logger)
    {
    }

    [KernelFunction("list_eventHub_workflows")]
    [Description("List the information of started workflows for event hub remediation")]
    public override Task<IReadOnlyList<WorkflowMetadata<LocalAuthAgentActivityInput>>> ListWorkflowsAsync()
    {
        return this.ListWorkflowsImplAsync();
    }

    [KernelFunction("start_local_auth_workflow")]
    [Description("Start the workflow to apply changes to local auth")]
    public override Task<string> StartAgentAsync(LocalAuthAgentActivityInput input)
    {
        return StartAgentImplAsync(input);
    }
}

