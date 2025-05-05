using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Agent.Logging;

namespace Agent.Runtime.SubAgents.Core.Steps;
public class OrchestrationAgentGenericExecuteStep : OrchestrationAgentStep
{
    public FunctionCallContent FunctionCall { get; set; }
    public Guid? ApprovalId { get; set; }

    public override async Task ExecuteAsync(TaskOrchestrationContext context, OrchestrationAgent agent)
    {
        var log = context.CreateReplaySafeLogger<OrchestrationAgentGenericExecuteStep>();
        Guid threadId = agent.ThreadId;

        log.LogInternalInformation("[{ThreadId}] Get other Function call: {FunctionCall}", threadId, FunctionCall.ToString());

        // For any other function call, check if there're arguments match with key in threadContext.Properties
        // if so, use the value from threadContext.Properties to set the arguments to avoid LLM hallucinations
        var args = new Dictionary<string, object>(FunctionCall.Arguments);

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
        var executionResult = await context.CallGenericExecuteActionActivityAsync(execInput);
        agent.ChatHistory.Add(executionResult.ChatMessage);

        if (executionResult.Is202Submit)
        {
            agent.Pending202Activities.Add(context.CallGenericExecute202ActionActivityAsync(execInput));
            log.LogInternalInformation("[{ThreadId}] 202 activity submitted: {ChatMessage}", threadId, executionResult.ChatMessage.ToString());
        }
    }
}
