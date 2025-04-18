using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;


namespace Agent.Runtime.SubAgents.Core.Steps;

public class OrchestrationAgentVisualizeAKSMicroserviceTopologyStep : OrchestrationAgentStep
{
    public FunctionCallContent FunctionCall { get; set; }

    public override async Task ExecuteAsync(TaskOrchestrationContext context, OrchestrationAgent agent)
    {
        var log = context.CreateReplaySafeLogger<OrchestrationAgentVisualizeAKSMicroserviceTopologyStep>();
        Guid threadId = agent.ThreadId;

        log.LogInformation("[{ThreadId}] Generating Visualization", threadId);

        // Extract arguments from the function call
        string resourceId = string.Empty;
        string _namespace = string.Empty;
        string deployment = string.Empty;
        int hops = 3; // Default value

        if (FunctionCall.Arguments.TryGetValue("AKSClusterResourceId", out var resourceIdObj) && resourceIdObj != null)
        {
            resourceId = resourceIdObj.ToString() ?? string.Empty;
        }
        if (FunctionCall.Arguments.TryGetValue("_namespace", out var namespaceObj) && namespaceObj != null)
        {
            _namespace = namespaceObj.ToString() ?? string.Empty;
        }
        if (FunctionCall.Arguments.TryGetValue("deploymentName", out var deploymentObj) && deploymentObj != null)
        {
            deployment = deploymentObj.ToString() ?? string.Empty;
        }
        log.LogInformation("[{ThreadId}] Generating Visualization with AKS {resourceIdObj}, namespace {_namespace}, name: {deployment}", threadId, resourceId, _namespace, deployment);


        // Create a new args dictionary with the threadId as a Guid
        var argsWithThreadId = new Dictionary<string, object>(FunctionCall.Arguments)
        {
            ["threadId"] = threadId
        };

        // Create a new function call with the updated arguments
        var updatedFunctionCall = new FunctionCallContent(
            FunctionCall.CallId,
            FunctionCall.Name,
            argsWithThreadId
        );

        // Execute the function with the updated arguments
        var execInput = new ExecuteActionInput(
            FunctionCallContent: updatedFunctionCall,
            ToolSignatures: agent.ToolSignatures);

        var executionResult = await context.CallGenericExecuteActionActivityAsync(execInput);
        agent.ChatHistory.Add(executionResult.ChatMessage);

        // Check if this is a long-running operation
        if (executionResult.Is202Submit)
        {
            agent.Pending202Activities.Add(context.CallGenericExecute202ActionActivityAsync(execInput));
            log.LogInformation("[{ThreadId}] 202 activity submitted for visualization: {ChatMessage}", threadId, executionResult.ChatMessage.ToString());
        }
    }
}
