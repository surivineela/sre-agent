using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.Core.Steps;

public class OrchestrationAgentGetActionDetailsStep : OrchestrationAgentStep
{
    public FunctionCallContent FunctionCall { get; set; }

    public override async Task ExecuteAsync(TaskOrchestrationContext context, OrchestrationAgent agent)
    {
        var log = context.CreateReplaySafeLogger<OrchestrationAgentGetActionDetailsStep>();
        Guid threadId = agent.ThreadId;
        Guid actionId = Guid.Empty;

        if (FunctionCall.Arguments.TryGetValue("actionId", out var actionIdObj) && actionIdObj != null)
        {
            if (Guid.TryParse(actionIdObj.ToString(), out var parsedActionId))
            {
                actionId = parsedActionId;
            }
        }
        log.LogInformation("[{ThreadId}] Getting action details for actionId: {ActionId}", threadId, actionId);

        if (actionId == Guid.Empty)
        {
            var errorContent = new FunctionResultContent(
                FunctionCall.CallId,
                "Invalid arguments. actionId is required.");
            agent.ChatHistory.Add(new ChatMessage(ChatRole.Tool, new[] { errorContent }));
            log.LogError("[{ThreadId}] Invalid actionId: {ActionId}", threadId, actionId);
        }
        else
        {
            try
            {
                log.LogInformation("[{ThreadId}] Retrieving action details for actionId: {ActionId}", threadId, actionId);
                // Call the get action details activity
                var action = await context.CallGetActionDetailsActivityAsync(new GetActionDetailsInput(
                    ThreadId: threadId,
                    ActionId: actionId
                ));
                log.LogInformation("[{ThreadId}] Action details retrieved: {Action}", threadId, action.ToString());

                // Return the action details as a JSON string
                var resultContent = new FunctionResultContent(
                    FunctionCall.CallId,
                    System.Text.Json.JsonSerializer.Serialize(action));
                agent.ChatHistory.Add(new ChatMessage(ChatRole.Tool, new[] { resultContent }));
            }
            catch (Exception ex)
            {
                // Handle case where action is not found
                var errorContent = new FunctionResultContent(
                    FunctionCall.CallId,
                    $"Error retrieving action: {ex.Message}");
                agent.ChatHistory.Add(new ChatMessage(ChatRole.Tool, new[] { errorContent }));
                log.LogError("[{ThreadId}] Error retrieving action details: {Error}", threadId, ex.Message);
            }
        }
    }
}
