using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Agent.Runtime.SubAgents.FunctionAppExecutionFailuresAgent;
using Microsoft.DurableTask.Client;
using Microsoft.SemanticKernel;

namespace Agent.Runtime.MetaAgent;
public class FunctionAppExecutionFailuresAgentPlugin : IMetaAgentFunctionAppExecutionFailuresAgentPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly FunctionAppExecutionFailuresAgentFactory _functionAppExecutionFailuresAgentFactory;

    public Guid? ThreadId { get; set; }

    public FunctionAppExecutionFailuresAgentPlugin(
        DurableTaskClient durableTaskClient,
        FunctionAppExecutionFailuresAgentFactory functionAppExecutionFailuresAgentFactory)
    {
        _durableTaskClient = durableTaskClient;
        _functionAppExecutionFailuresAgentFactory = functionAppExecutionFailuresAgentFactory;
    }

    [KernelFunction("check_function_app_execution_failures")]
    [Description("Start the workflow to investigate execution failures in an Azure Function app")]
    public async Task<string> StartFunctionAppExecutionFailuresAgent(
        [Description("ARM resource id for the Function app to investigate")] string functionAppResourceId)
    {
        if (ThreadId == null)
        {
            throw new InvalidOperationException("Thread context is not set. Please set the context before starting the workflow.");
        }

        try
        {
            var instanceId = await _functionAppExecutionFailuresAgentFactory.StartOrchestration(functionAppResourceId, ThreadId.Value);
            return $"A workflow has been started to investigate execution failures in your Function app: {instanceId}";
        }
        catch (Exception ex)
        {
            // Log the exception or handle it as needed
            throw new InvalidOperationException("Failed to start the orchestration for execution failures investigation.", ex);
        }
    }
}
