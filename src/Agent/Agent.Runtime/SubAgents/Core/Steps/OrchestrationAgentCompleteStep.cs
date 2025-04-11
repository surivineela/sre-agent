using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;


namespace Agent.Runtime.SubAgents.Core.Steps;

public class OrchestrationAgentCompleteStep : OrchestrationAgentStep
{
    public FunctionCallContent FunctionCall { get; set; } 

    //use this once function calling is enabled
    //public string Message { get; set; }

    public override async Task ExecuteAsync(TaskOrchestrationContext context, OrchestrationAgent agent)
    {
        var log = context.CreateReplaySafeLogger<OrchestrationAgentCompleteStep>();
        Guid threadId = agent.ThreadContext.ThreadId;

        agent.Done = true;

        var message = string.Empty;
        if (FunctionCall.Arguments.TryGetValue("message", out var messageObj) && messageObj != null)
        {
            message = messageObj.ToString() ?? string.Empty;
        }

        log.LogInformation("[{ThreadId}] Marking plan as complete with message: {Message}", threadId, message);

        // Call the communication activity
        await context.CallUpdateThreadWithAgentMessageActivityAsync(new UpdateThreadWithAgentMessageInput(
            ThreadContext: agent.ThreadContext,
            InstanceId: context.InstanceId,
            Message: message
        ));

        var resultContent = new FunctionResultContent(FunctionCall.CallId, "Plan marked as complete.");
        agent.ChatHistory.Add(new ChatMessage(ChatRole.Tool, new[] { resultContent }));
        log.LogInformation("[{ThreadId}] Marking plan as complete", threadId);
    }
}
