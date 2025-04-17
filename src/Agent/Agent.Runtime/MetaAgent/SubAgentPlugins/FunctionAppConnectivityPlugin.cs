using System.ComponentModel;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents.FunctionAppConnectivityAgent;
using Microsoft.DurableTask.Client;
using Microsoft.SemanticKernel;


namespace Agent.Runtime.MetaAgent;
public class FunctionAppConnectivityPlugin : IMetaAgentFunctionAppConnectivityPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly FunctionAppConnectivityAgentFactory _functionAppConnectivityAgentFactory;

    public ThreadContext? Context { get; set; }

    public FunctionAppConnectivityPlugin(
        DurableTaskClient durableTaskClient,
        FunctionAppConnectivityAgentFactory functionAppConnectivityAgentFactory)
    {
        _durableTaskClient = durableTaskClient;
        _functionAppConnectivityAgentFactory = functionAppConnectivityAgentFactory;
    }

    [KernelFunction("check_function_app_connectivity_to_storage_account")]
    [Description("Start the workflow to check the connectivity between Azure Function app and web or Azure Storage Account")]
    public async Task<string> StartFunctionAppConnectivityAgent(
        [Description("Inputs to the agent that includes arm resource id for the Function app to investigate, a list of tools and thread context.")] FunctionAppConnectivityAgentInput input)
    {
        if (Context == null)
        {
            throw new InvalidOperationException("Thread context is not set. Please set the context before starting the workflow.");
        }

        var instanceId = await _functionAppConnectivityAgentFactory.StartOrchestration(input, Context);
        return $"A workflow has been started to check connectivity from Function app to the target destination: {instanceId}";
    }
}
