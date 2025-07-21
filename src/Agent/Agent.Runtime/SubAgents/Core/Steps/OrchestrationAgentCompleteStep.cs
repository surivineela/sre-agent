using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Agent.Logging;

namespace Agent.Runtime.SubAgents.Core.Steps;

public class OrchestrationAgentCompleteStep : OrchestrationAgentStep
{
    public FunctionCallContent? FunctionCall { get; set; } 

    //use this once function calling is enabled
    //public string Message { get; set; }

    public override async Task ExecuteAsync(TaskOrchestrationContext context, OrchestrationAgent agent)
    {
        if (FunctionCall == null)
        {
            throw new ArgumentNullException(nameof(FunctionCall), "FunctionCall cannot be null.");
        }

        var log = context.CreateReplaySafeLogger<OrchestrationAgentCompleteStep>();
        Guid threadId = agent.ThreadId;

        agent.Done = true;

        var message = string.Empty;
        if (FunctionCall.Arguments != null && FunctionCall.Arguments.TryGetValue("message", out var messageObj) && messageObj != null)
        {
            message = messageObj.ToString() ?? string.Empty;
        }

        log.LogInternalInformation("[{ThreadId}] Marking plan as complete with message: {Message}", threadId, message);

        // Call the communication activity
        await context.CallUpdateThreadWithAgentMessageActivityAsync(new UpdateThreadWithAgentMessageInput(
            ThreadId: agent.ThreadId,
            InstanceId: context.InstanceId,
            Message: message
        ));

        var resultContent = new FunctionResultContent(FunctionCall.CallId, "Plan marked as complete.");
        agent.ChatHistory.Add(new ChatMessage(ChatRole.Tool, new[] { resultContent }));
        log.LogInternalInformation("[{ThreadId}] Marking plan as complete", threadId);
    }
}
