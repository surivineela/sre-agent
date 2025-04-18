using Agent.Core.Models.Api.v1;
using Agent.Plugins;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;


namespace Agent.Runtime.SubAgents.Core.Steps;

public class OrchestrationAgentUserCommunicationStep : OrchestrationAgentStep
{
    public FunctionCallContent FunctionCall { get; set; }

    //use this once function calling is enabled
    //public string Message { get; set; }

    public override async Task ExecuteAsync(TaskOrchestrationContext context, OrchestrationAgent agent)
    {
        var log = context.CreateReplaySafeLogger<OrchestrationAgentUserCommunicationStep>();
        Guid threadId = agent.ThreadId;

        if (FunctionCall.Name == nameof(ControlFlowPluginDefinition.AskUserForInput))
        {
            agent.ResponseFromUserIsPending = true;
            log.LogInformation("[{ThreadId}] User response pending", threadId);
            var resultContent = new FunctionResultContent(FunctionCall.CallId, "Question sent to user.");
            agent.ChatHistory.Add(new ChatMessage(ChatRole.Tool, new[] { resultContent }));
        }
        else
        {
            var resultContent = new FunctionResultContent(FunctionCall.CallId, "User notified.");
            agent.ChatHistory.Add(new ChatMessage(ChatRole.Tool, new[] { resultContent }));
        }

        log.LogInformation("[{ThreadId}] Notifying user", threadId);
        // Fix: Extract message from the arguments dictionary
        string message = string.Empty;
        if (FunctionCall.Arguments.TryGetValue("message", out var messageObj) && messageObj != null)
        {
            message = messageObj.ToString() ?? string.Empty;
        }
        log.LogInformation("[{ThreadId}] Message to notify user: {Message}", threadId, message);

        // Call the communication activity
        await context.CallUpdateThreadWithAgentMessageActivityAsync(new UpdateThreadWithAgentMessageInput(
            ThreadId: agent.ThreadId,
            InstanceId: context.InstanceId,
            Message: message
        ));
        log.LogInformation("[{ThreadId}] User notified with message: {Message}", threadId, message);
    }
}
