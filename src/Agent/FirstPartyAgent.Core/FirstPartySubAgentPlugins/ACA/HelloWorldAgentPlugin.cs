// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.MetaAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.HelloWorldAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace FirstPartyAgent.Core.FirstPartySubAgentPlugins.ACA;

// [MENDATORY]
public class HelloWorldAgentPlugin : SimpleResourceSubAgentPluginBase<HelloWorldAgentFactory, HelloWorldAgent, HelloWorldAgentInput, HelloWorldAgentActivity, HelloWorldAgentActivityInput>
{
    public HelloWorldAgentPlugin(
        DurableTaskClient durableTaskClient,
        HelloWorldAgentFactory factory,
        ILogger<HelloWorldAgent> logger)
        : base(durableTaskClient, factory, logger)
    {
    }

    // There MUST be always these two Kernel functions in the plugin for MetaAgent to call this 'HelloWorldAgent' sub-agent.
    // Note: KernelFunctions required for implementing 'HelloWorldAgent' sub-agent tool capabilities MUST be defined inside <reference>FirstPartyAgent.Core.Plugins.Implementation.HelloWorldPlugin</reference>
    [KernelFunction("list_hello_world_workflows")]
    [Description("List the information of started workflows for hello world resources remediation")]
    public override Task<IReadOnlyList<WorkflowMetadata<HelloWorldAgentActivityInput>>> ListWorkflowsAsync()
    {
        return ListWorkflowsImplAsync();
    }

    [KernelFunction("start_hello_world_workflow")]
    [Description("Start the workflow to apply changes to hello world resource")]
    public override Task<string> StartAgentAsync(HelloWorldAgentActivityInput input)
    {
        return StartAgentImplAsync(input);
    }
}
