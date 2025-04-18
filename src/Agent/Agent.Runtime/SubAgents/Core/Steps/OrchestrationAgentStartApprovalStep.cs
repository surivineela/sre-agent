using Agent.Core.Models;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;


namespace Agent.Runtime.SubAgents.Core.Steps;

public class OrchestrationAgentStartApprovalStep : OrchestrationAgentStep
{
    public FunctionCallContent FunctionCall { get; set; }

    public override async Task ExecuteAsync(TaskOrchestrationContext context, OrchestrationAgent agent)
    {
        var log = context.CreateReplaySafeLogger<OrchestrationAgentStartApprovalStep>();
        Guid threadId = agent.ThreadId;

        log.LogInformation("[{ThreadId}] Starting approval flow", threadId);

        var operationName = FunctionCall.Arguments["operationName"]?.ToString() ?? "operation";
        var approvalInstanceId = $"approval-{context.NewGuid()}";
        var approvalInput = new ApprovalInput(context.InstanceId, operationName, threadId.ToString(), approvalInstanceId);
        var description = FunctionCall.Arguments["description"]?.ToString() ?? "Pending approval";

        log.LogInformation("[{ThreadId}] Trying to generate approvalLink for operation: {OperationName} approval instanceId: {approvalInstanceId}", threadId, operationName, approvalInstanceId);

        // Generate approval link with the new activity
        string approvalLink = await context.CallActivityAsync<string>(
            nameof(GenerateApprovalLinkActivity),
            (approvalInstanceId, operationName, description)
        );
        log.LogInformation("[{ThreadId}] Approval link generated: {ApprovalLink} for {approvalInstanceId}. Trying to notify user.", threadId, approvalLink, approvalInstanceId);

        // Notify user about approval with the generated link
        await context.CallUpdateThreadWithAgentMessageActivityAsync(new UpdateThreadWithAgentMessageInput(
            ThreadId: agent.ThreadId,
            InstanceId: context.InstanceId,
            Message: $"Approval required for: {operationName}. [Click here to approve]({approvalLink})"
        ));
        log.LogInformation("[{ThreadId}] User notified about approval with link: {ApprovalLink}. Trying to start ApprovalOrchestration", threadId, approvalLink);

        // Start the approval suborchestration
        agent.WaitTask = context.CallSubOrchestratorAsync<ApprovalStatus>(
            new TaskName(nameof(ApprovalOrchestration)),
            approvalInput,
            new SubOrchestrationOptions { InstanceId = approvalInstanceId }
        );

        var resultContent = new FunctionResultContent(
            FunctionCall.CallId,
            $"Approval flow started for operation {operationName}");
        agent.ChatHistory.Add(new ChatMessage(ChatRole.Tool, new[] { resultContent }));
    }
}

public class OrchestrationAgentGenericExecuteStep : OrchestrationAgentStep
{
    public FunctionCallContent FunctionCall { get; set; }

    public override async Task ExecuteAsync(TaskOrchestrationContext context, OrchestrationAgent agent)
    {
        var log = context.CreateReplaySafeLogger<OrchestrationAgentGenericExecuteStep>();
        Guid threadId = agent.ThreadId;

        log.LogInformation("[{ThreadId}] Get other Function call: {FunctionCall}", threadId, FunctionCall.ToString());

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
            FunctionCallContent: updatedFunctionCall,
            ToolSignatures: agent.ToolSignatures);
        var executionResult = await context.CallGenericExecuteActionActivityAsync(execInput);
        agent.ChatHistory.Add(executionResult.ChatMessage);

        if (executionResult.Is202Submit)
        {
            agent.Pending202Activities.Add(context.CallGenericExecute202ActionActivityAsync(execInput));
            log.LogInformation("[{ThreadId}] 202 activity submitted: {ChatMessage}", threadId, executionResult.ChatMessage.ToString());
        }
    }
}
