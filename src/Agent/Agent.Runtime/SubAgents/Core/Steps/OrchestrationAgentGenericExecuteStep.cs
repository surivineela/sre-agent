using Agent.Core.Models.Api.v1;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.Core.Steps;
public class OrchestrationAgentGenericExecuteStep : OrchestrationAgentStep
{
    public FunctionCallContent? FunctionCall { get; set; }
    public Guid? ApprovalId { get; set; }

    public override async Task ExecuteAsync(TaskOrchestrationContext context, OrchestrationAgent agent)
    {
        if (FunctionCall == null)
        {
            throw new ArgumentNullException(nameof(FunctionCall), "FunctionCall cannot be null.");
        }

        var log = context.CreateReplaySafeLogger<OrchestrationAgentGenericExecuteStep>();
        Guid threadId = agent.ThreadId;

        log.LogInternalInformation("[{ThreadId}] Get other Function call: {FunctionCall}", threadId, FunctionCall.ToString());

        // For any other function call, check if there're arguments match with key in threadContext.Properties
        // if so, use the value from threadContext.Properties to set the arguments to avoid LLM hallucinations
        var args = new Dictionary<string, object?>(FunctionCall.Arguments ?? new Dictionary<string, object?>());

        // Create a new function call with the updated arguments
        var updatedFunctionCall = new FunctionCallContent(
            FunctionCall.CallId,
            FunctionCall.Name,
            args
        );

        // For any other function call, defer to the derived implementation
        var execInput = new ExecuteActionInput(
            ThreadId: threadId,
            ApprovalId: ApprovalId,
            FunctionCallContent: updatedFunctionCall,
            ToolSignatures: agent.ToolSignatures);

        await agent.RecordActionIfNeeded(FunctionCall, ActionStatus.InProgress);

        var executionResult = await context.CallGenericExecuteActionActivityAsync(execInput);
        agent.ChatHistory.Add(executionResult.ChatMessage);

        if (executionResult.Is202Submit)
        {
            agent.Pending202Activities.Add(context.CallGenericExecute202ActionActivityAsync(execInput));
            log.LogInternalInformation("[{ThreadId}] 202 activity submitted: {ChatMessage}", threadId, executionResult.ChatMessage.ToString());
        }

        await agent.RecordActionIfNeeded(FunctionCall, executionResult.Succeeded ? ActionStatus.Completed : ActionStatus.Failed);
    }
}
