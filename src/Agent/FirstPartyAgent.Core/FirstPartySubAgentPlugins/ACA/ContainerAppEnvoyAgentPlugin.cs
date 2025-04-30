// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.MetaAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppEnvoyAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace FirstPartyAgent.Core.FirstPartySubAgentPlugins.ACA;
public class ContainerAppEnvoyAgentPlugin : SimpleResourceSubAgentPluginBase<ContainerAppEnvoyAgentFactory,ContainerAppEnvoyAgent,ContainerAppEnvoyAgentInput,ContainerAppEnvoyAgentActivity,ContainerAppEnvoyAgentActivityInput>
{
    public ContainerAppEnvoyAgentPlugin(
        DurableTaskClient durableTaskClient,
        ContainerAppEnvoyAgentFactory factory,
        ILogger<ContainerAppEnvoyAgent> logger)
        : base(durableTaskClient, factory, logger)
    {
    }

    [KernelFunction("list_container_app_envoy_workflows")]
    [Description("List the information of started workflows for container app envoy issue investigation.")]
    public override Task<IReadOnlyList<WorkflowMetadata<ContainerAppEnvoyAgentActivityInput>>> ListWorkflowsAsync()
    {
        return ListWorkflowsImplAsync();
    }

    [KernelFunction("start_container_app_envoy_workflow")]
    [Description("Start the workflow to investigate container app envoy issue.")]
    public override Task<string> StartAgentAsync(ContainerAppEnvoyAgentActivityInput input)
    {
        return StartAgentImplAsync(input);
    }
}
