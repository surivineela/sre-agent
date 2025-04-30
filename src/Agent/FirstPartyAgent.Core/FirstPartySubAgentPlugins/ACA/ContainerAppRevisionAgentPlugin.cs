// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.MetaAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.RevisionAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace FirstPartyAgent.Core.FirstPartySubAgentPlugins.ACA;

// [MENDATORY]
public class ContainerAppRevisionAgentPlugin : SimpleResourceSubAgentPluginBase<ContainerAppRevisionAgentFactory, ContainerAppRevisionAgent, RevisionAgentInput, ContainerAppRevisionAgentActivity, ContainerAppRevisionAgentActivityInput>
{
    public ContainerAppRevisionAgentPlugin(
        DurableTaskClient durableTaskClient,
        ContainerAppRevisionAgentFactory factory,
        ILogger<ContainerAppRevisionAgent> logger)
        : base(durableTaskClient, factory, logger)
    {
    }

    // There MUST be always these two Kernel functions in the plugin for MetaAgent to call this 'ContainerAppRevisionAgent' sub-agent.
    // Note: KernelFunctions required for implementing 'ContainerAppRevisionAgent' sub-agent tool capabilities MUST be defined inside <reference>FirstPartyAgent.Core.Plugins.Implementation.RevisionPlugin</reference>
    [KernelFunction("list_container_app_revision_workflows")]
    [Description("List the information of started workflows for container app revision resources remediation")]
    public override Task<IReadOnlyList<WorkflowMetadata<ContainerAppRevisionAgentActivityInput>>> ListWorkflowsAsync()
    {
        return ListWorkflowsImplAsync();
    }

    [KernelFunction("start_container_app_revision_workflow")]
    [Description("Start the workflow to apply changes to container app revision resource")]
    public override Task<string> StartAgentAsync(ContainerAppRevisionAgentActivityInput input)
    {
        return StartAgentImplAsync(input);
    }
}
