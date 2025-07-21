using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Agent.Logging;

namespace Agent.Runtime.SubAgents.Core.Steps;

public class OrchestrationAgentVisualizeAppComponentsStep : OrchestrationAgentStep
{
    public FunctionCallContent? FunctionCall { get; set; }

    public override async Task ExecuteAsync(TaskOrchestrationContext context, OrchestrationAgent agent)
    {
        if (FunctionCall == null)
        {
            throw new ArgumentNullException(nameof(FunctionCall), "FunctionCall cannot be null.");
        }

        var log = context.CreateReplaySafeLogger<OrchestrationAgentVisualizeAppComponentsStep>();
        Guid threadId = agent.ThreadId;

        log.LogInternalInformation("[{ThreadId}] Generating Visualization", threadId);

        // Extract arguments from the function call
        string resourceId = string.Empty;
        int hops = 3; // Default value

        if (FunctionCall.Arguments != null)
        {
            if (FunctionCall.Arguments.TryGetValue("resourceId", out var resourceIdObj) && resourceIdObj != null)
            {
                resourceId = resourceIdObj.ToString() ?? string.Empty;
            }

            if (FunctionCall.Arguments.TryGetValue("hops", out var hopsObj) && hopsObj != null)
            {
                if (int.TryParse(hopsObj.ToString(), out var parsedHops))
                {
                    hops = parsedHops;
                }
            }
        }

        // Create a new args dictionary with the threadId as a Guid
        var argsWithThreadId = new Dictionary<string, object?>(FunctionCall.Arguments ?? new Dictionary<string, object?>())
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
            ThreadId: threadId,
            ApprovalId: null,
            FunctionCallContent: updatedFunctionCall,
            ToolSignatures: agent.ToolSignatures);

        var executionResult = await context.CallGenericExecuteActionActivityAsync(execInput);
        agent.ChatHistory.Add(executionResult.ChatMessage);

        // Check if this is a long-running operation
        if (executionResult.Is202Submit)
        {
            agent.Pending202Activities.Add(context.CallGenericExecute202ActionActivityAsync(execInput));
            log.LogInternalInformation("[{ThreadId}] 202 activity submitted for visualization: {ChatMessage}", threadId, executionResult.ChatMessage.ToString());
        }
    }
}
