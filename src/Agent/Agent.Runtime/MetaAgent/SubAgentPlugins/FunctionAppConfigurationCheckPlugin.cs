using System.ComponentModel;
using Agent.Runtime.SubAgents.FunctionAppConfigurationCheck;
using Microsoft.DurableTask.Client;
using Microsoft.SemanticKernel;

namespace Agent.Runtime.MetaAgent;

public class FunctionAppConfigurationCheckPlugin : IMetaAgentFunctionAppConfigurationCheckAgentPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly FunctionAppConfigurationCheckAgentFactory _functionAppConfigurationCheckAgentFactory;

    public Guid? ThreadId { get; set; }

    public FunctionAppConfigurationCheckPlugin(
        DurableTaskClient durableTaskClient,
        FunctionAppConfigurationCheckAgentFactory functionAppConfigurationCheckAgentFactory)
    {
        _durableTaskClient = durableTaskClient;
        _functionAppConfigurationCheckAgentFactory = functionAppConfigurationCheckAgentFactory;
    }

    [KernelFunction("check_function_app_configuration")]
    [Description("Start the workflow to check and optimize the configuration of an Azure Function App")]
    public async Task<string> StartFunctionAppConfigurationCheckAgent(
        [Description("The Azure resource ID of the Function App to check configuration for")] string functionAppResourceId)
    {
        if (ThreadId == null)
        {
            throw new InvalidOperationException("Thread context is not set. Please set the context before starting the workflow.");
        }

        var instanceId = await _functionAppConfigurationCheckAgentFactory.StartOrchestration(functionAppResourceId, ThreadId.Value);
        return $"A workflow has been started to check and optimize the configuration for the Function App: {instanceId}";
    }
}
