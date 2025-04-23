// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using Agent.Runtime.MetaAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppsQuotaAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;


namespace FirstPartyAgent.Core.FirstPartySubAgentPlugins.ACA;

public class ContainerAppsQuotaAgentPlugin : SimpleResourceSubAgentPluginBase<ContainerAppsQuotaAgentFactory, ContainerAppsQuotaAgent, ContainerAppsQuotaAgentInput, ContainerAppsQuotaAgentActivity, ContainerAppsQuotaAgentActivityInput>
{
    public ContainerAppsQuotaAgentPlugin(
        DurableTaskClient durableTaskClient,
        ContainerAppsQuotaAgentFactory containerAppsQuotaAgentFactory,
        ILogger<ContainerAppsQuotaAgent> logger)
        : base(durableTaskClient, containerAppsQuotaAgentFactory, logger)
    {
    }

    [KernelFunction("list_containerapps_quota_workflow")]
    [Description("List the information of started workflow for container apps quota request")]
    public override Task<IReadOnlyList<WorkflowMetadata<ContainerAppsQuotaAgentActivityInput>>> ListWorkflowsAsync()
    {
        return ListWorkflowsImplAsync();
    }

    [KernelFunction("start_container_apps_quota_workflow")]
    [Description("Start the workflow to process azure container apps quota request.")]
    public override Task<string> StartAgentAsync(ContainerAppsQuotaAgentActivityInput input)
    {
        return StartAgentImplAsync(input);
    }
}
