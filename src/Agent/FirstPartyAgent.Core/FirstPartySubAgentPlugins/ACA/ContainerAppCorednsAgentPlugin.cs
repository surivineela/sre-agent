// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.MetaAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.CorednsAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace FirstPartyAgent.Core.FirstPartySubAgentPlugins.ACA;

// [MENDATORY]
public class ContainerAppCorednsAgentPlugin : SimpleResourceSubAgentPluginBase<ContainerAppCorednsAgentFactory, ContainerAppCorednsAgent, CorednsAgentInput, ContainerAppCorednsAgentActivity, ContainerAppCorednsAgentActivityInput>
{
    public ContainerAppCorednsAgentPlugin(
        DurableTaskClient durableTaskClient,
        ContainerAppCorednsAgentFactory factory,
        ILogger<ContainerAppCorednsAgent> logger)
        : base(durableTaskClient, factory, logger)
    {
    }

    // There MUST be always these two Kernel functions in the plugin for MetaAgent to call this 'ContainerAppCorednsAgent' sub-agent.
    // Note: KernelFunctions required for implementing 'ContainerAppCorednsAgent' sub-agent tool capabilities MUST be defined inside <reference>FirstPartyAgent.Core.Plugins.Implementation.CorednsPlugin</reference>
    [KernelFunction("list_container_app_Coredns_workflows")]
    [Description("List the information of started workflows for container app Coredns resources remediation")]
    public override Task<IReadOnlyList<WorkflowMetadata<ContainerAppCorednsAgentActivityInput>>> ListWorkflowsAsync()
    {
        return ListWorkflowsImplAsync();
    }

    [KernelFunction("start_container_app_Coredns_workflow")]
    [Description("Start the workflow to apply changes to container app Coredns resource")]
    public override Task<string> StartAgentAsync(ContainerAppCorednsAgentActivityInput input)
    {
        return StartAgentImplAsync(input);
    }
}
